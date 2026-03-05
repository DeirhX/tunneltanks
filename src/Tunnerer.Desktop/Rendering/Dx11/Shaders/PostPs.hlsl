Texture2D sceneTex : register(t0);
Texture2D auxTex : register(t1);
SamplerState s0 : register(s0);

cbuffer PostParams : register(b0)
{
    float2 TexelSize;
    float PixelScale;
    float Time;
    float2 WorldSize;
    float2 CameraPixels;
    float2 ViewSize;
    float UseTerrainAux;
    float BloomThreshold;
    float BloomStrength;
    float BloomWeightCenter;
    float BloomWeightAxis;
    float BloomWeightDiagonal;
    float VignetteStrength;
    float EdgeLightStrength;
    float EdgeLightBias;
    float4 TankHeatGlowColor;
    float4 TerrainHeatGlowColorAndThreshold;
    float TerrainMaskEdgeStrength;
    float TerrainMaskCaveDarken;
    float TerrainMaskSolidLift;
    float TerrainMaskOutlineDarken;
    float TerrainMaskRimLift;
    float TerrainMaskBoundaryScale;
    float VignetteInnerRadius;
    float VignetteOuterRadius;
    float Quality;
    float4 MaterialEmissiveEnergy;
    float4 MaterialEmissiveScorched;
    float4 MaterialEmissivePulse;
    float4 NativeContinuousParams;
    float4 LightDir;     // xyz = direction, w = NormalStrength
    float4 HalfVector;   // xyz = half-vector, w = MicroNormalStrength
    float4 LightParams;  // x = Ambient, y = DiffuseWeight, z = Shininess, w = SpecularIntensity
    float TankGlowCount;
    float4 TankGlow[8];
};

float3 bright(float3 c) { return max(c - float3(BloomThreshold, BloomThreshold, BloomThreshold), 0.0); }

// GPU hash noise for procedural micro-normal perturbation (matches CPU TexTileDensity = 0.08)
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbmNoise(float2 p, int octaves)
{
    float val = 0.0;
    float amp = 0.5;
    for (int i = 0; i < octaves; i++)
    {
        val += amp * valueNoise(p);
        p *= 2.17;
        amp *= 0.5;
    }
    return val;
}

float3 HeatRamp(float t)
{
    t = saturate(t);
    // Non-black start so low temperatures remain visibly warm.
    float3 c0 = float3(0.14, 0.01, 0.00);
    float3 c1 = float3(0.75, 0.02, 0.00);
    float3 c2 = float3(0.95, 0.26, 0.02);
    float3 c3 = float3(1.00, 0.78, 0.10);
    float3 c4 = float3(1.00, 1.00, 0.80);

    // Smoothly blend through anchor colors to avoid visible band edges.
    float w1 = smoothstep(0.16, 0.42, t);
    float w2 = smoothstep(0.38, 0.66, t);
    float w3 = smoothstep(0.68, 0.92, t);
    float w4 = smoothstep(0.90, 1.00, t);

    float3 col = lerp(c0, c1, w1);
    col = lerp(col, c2, w2);
    col = lerp(col, c3, w3);
    col = lerp(col, c4, w4);
    return col;
}

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    // Tank heat haze: distort scene lookup around hot tanks before post-lighting.
    float2 hazeOffset = float2(0.0, 0.0);
    float outlineHeatMask = 0.0;
    float distortionEnabled = step(0.5, TankHeatGlowColor.a);
    [loop]
    for (int i = 0; i < 8; i++)
    {
        if (distortionEnabled <= 0.0) break;
        if (i >= (int)TankGlowCount) break;
        float4 g = TankGlow[i];
        float heatT = saturate(g.w);
        float2 d = uv - g.xy;
        float2 dPx = d * ViewSize;
        float dist2 = dot(dPx, dPx);
        float radiusPx = max(1e-3, g.z * max(ViewSize.x, ViewSize.y));
        float radius2 = radiusPx * radiusPx;
        float falloff = saturate(1.0 - dist2 / radius2);
        if (falloff <= 0.0) continue;

        float distNorm = sqrt(saturate(1.0 - falloff));
        float tankEdgeBand = smoothstep(0.34, 0.16, distNorm) * (1.0 - smoothstep(0.16, 0.03, distNorm));
        float2 swirlDirPx = normalize(float2(-dPx.y, dPx.x) + float2(1e-4, -1e-4));
        float shimmer =
            sin(Time * 14.0 + (uv.x * 210.0 + uv.y * 170.0) + i * 1.7) * 0.65 +
            sin(Time * 22.0 + (uv.x * 120.0 - uv.y * 140.0) + i * 2.3) * 0.35;
        float heatGate = smoothstep(0.40, 0.75, heatT);
        float hazeStrengthPx = heatGate * (0.8 + 4.0 * heatT) * (0.35 + 0.65 * tankEdgeBand) * falloff;
        hazeOffset += swirlDirPx * shimmer * hazeStrengthPx * TexelSize;

        // Overheat(50+) mask derived from glow intensity; used for outline-only shimmer below.
        float over50 = smoothstep(0.78, 0.95, heatT);
        outlineHeatMask += over50 * falloff;

        // Dedicated hot-tank edge wobble so tank outlines visibly shimmer once heat is high.
        float2 tankEdgeDirPx = normalize(float2(dPx.y, -dPx.x) + float2(1e-4, -1e-4));
        float tankWave =
            sin(Time * 31.0 + distNorm * 96.0 + i * 2.4) * 0.65 +
            sin(Time * 19.0 - distNorm * 71.0 + i * 1.3) * 0.35;
        float edgeWobblePx = over50 * tankEdgeBand * (0.40 + 2.20 * heatT);
        hazeOffset += tankEdgeDirPx * tankWave * edgeWobblePx * TexelSize;

    }
    outlineHeatMask = saturate(outlineHeatMask);

    // Extra distortion on geometric outlines (terrain+tanks) once tanks are sufficiently hot.
    if (distortionEnabled > 0.0 && outlineHeatMask > 0.0)
    {
        float2 tx = float2(TexelSize.x, 0.0);
        float2 ty = float2(0.0, TexelSize.y);
        float3 cL = sceneTex.Sample(s0, uv - tx).rgb;
        float3 cR = sceneTex.Sample(s0, uv + tx).rgb;
        float3 cU = sceneTex.Sample(s0, uv - ty).rgb;
        float3 cD = sceneTex.Sample(s0, uv + ty).rgb;
        float aL = sceneTex.Sample(s0, uv - tx).a;
        float aR = sceneTex.Sample(s0, uv + tx).a;
        float aU = sceneTex.Sample(s0, uv - ty).a;
        float aD = sceneTex.Sample(s0, uv + ty).a;

        float lL = dot(cL, float3(0.299, 0.587, 0.114));
        float lR = dot(cR, float3(0.299, 0.587, 0.114));
        float lU = dot(cU, float3(0.299, 0.587, 0.114));
        float lD = dot(cD, float3(0.299, 0.587, 0.114));
        float2 edgeGrad = float2(lR - lL, lD - lU);

        float edgeLum = saturate(length(edgeGrad) * 3.5);
        float edgeAlpha = saturate((abs(aR - aL) + abs(aD - aU)) * 2.8);
        float outlineEdge = max(edgeLum, edgeAlpha);

        // Shimmer tangent to the edge gradient so silhouettes appear to wobble.
        float2 edgeDir = normalize(float2(edgeGrad.y, -edgeGrad.x) + float2(1e-4, -1e-4));
        float outlineWave =
            sin(Time * 28.0 + uv.x * 260.0 - uv.y * 220.0) * 0.60 +
            sin(Time * 17.0 + uv.x * 120.0 + uv.y * 145.0) * 0.40;
        hazeOffset += edgeDir * outlineWave * (outlineHeatMask * outlineEdge * 0.012);
    }

    float2 hazeUv = clamp(uv + hazeOffset, TexelSize * 0.5, 1.0 - TexelSize * 0.5);
    float4 sceneSample = sceneTex.Sample(s0, hazeUv);
    float3 baseColor = sceneSample.rgb;
    float3 color = baseColor;
    float terrainFactor = step(0.999, sceneSample.a);

    if (Quality >= 1.0)
    {
        float2 tx = float2(TexelSize.x, 0.0);
        float2 ty = float2(0.0, TexelSize.y);
        float2 d1 = float2(TexelSize.x, TexelSize.y);
        float2 d2 = float2(TexelSize.x, -TexelSize.y);
        float3 bloom = bright(baseColor) * BloomWeightCenter;
        bloom += bright(sceneTex.Sample(s0, uv + tx).rgb) * BloomWeightAxis;
        bloom += bright(sceneTex.Sample(s0, uv - tx).rgb) * BloomWeightAxis;
        bloom += bright(sceneTex.Sample(s0, uv + ty).rgb) * BloomWeightAxis;
        bloom += bright(sceneTex.Sample(s0, uv - ty).rgb) * BloomWeightAxis;
        bloom += bright(sceneTex.Sample(s0, uv + d1).rgb) * BloomWeightDiagonal;
        bloom += bright(sceneTex.Sample(s0, uv - d1).rgb) * BloomWeightDiagonal;
        bloom += bright(sceneTex.Sample(s0, uv + d2).rgb) * BloomWeightDiagonal;
        bloom += bright(sceneTex.Sample(s0, uv - d2).rgb) * BloomWeightDiagonal;
        color += bloom * BloomStrength;
    }

    if (Quality >= 2.0)
    {
        float d = distance(uv, float2(0.5, 0.5));
        float vig = 1.0 - smoothstep(VignetteInnerRadius, VignetteOuterRadius, d) * VignetteStrength;
        color *= vig;
    }

    if (Quality >= 1.0)
    {
        float l = dot(sceneTex.Sample(s0, uv + float2(-TexelSize.x, 0.0)).rgb, float3(0.299, 0.587, 0.114));
        float r = dot(sceneTex.Sample(s0, uv + float2(TexelSize.x, 0.0)).rgb, float3(0.299, 0.587, 0.114));
        float u = dot(sceneTex.Sample(s0, uv + float2(0.0, -TexelSize.y)).rgb, float3(0.299, 0.587, 0.114));
        float d = dot(sceneTex.Sample(s0, uv + float2(0.0, TexelSize.y)).rgb, float3(0.299, 0.587, 0.114));
        float edge = abs(r - l) + abs(d - u);
        float edgeLift = max(0.0, edge - EdgeLightBias) * EdgeLightStrength;
        color += edgeLift.xxx;
    }

    if (UseTerrainAux > 0.5 && PixelScale > 0.0)
    {
        float2 screenPx = uv * ViewSize;
        float2 worldCell = (CameraPixels + screenPx) / PixelScale;
        float2 auxUv = worldCell / WorldSize;
        float2 mTexel = float2(1.0 / WorldSize.x, 1.0 / WorldSize.y);
        float4 a0 = auxTex.Sample(s0, auxUv);
        float4 ax1 = auxTex.Sample(s0, auxUv + float2(mTexel.x, 0.0));
        float4 ax2 = auxTex.Sample(s0, auxUv - float2(mTexel.x, 0.0));
        float4 ay1 = auxTex.Sample(s0, auxUv + float2(0.0, mTexel.y));
        float4 ay2 = auxTex.Sample(s0, auxUv - float2(0.0, mTexel.y));
        float4 ad1 = auxTex.Sample(s0, auxUv + float2(mTexel.x, mTexel.y));
        float4 ad2 = auxTex.Sample(s0, auxUv + float2(mTexel.x, -mTexel.y));
        float4 ad3 = auxTex.Sample(s0, auxUv + float2(-mTexel.x, mTexel.y));
        float4 ad4 = auxTex.Sample(s0, auxUv + float2(-mTexel.x, -mTexel.y));

        float m0 = a0.g;
        float mx1 = ax1.g;
        float mx2 = ax2.g;
        float my1 = ay1.g;
        float my2 = ay2.g;

        // Sobel gradient of the smooth SDF — used for edge detection and normals
        float gx = (ad2.g + 2.0 * mx1 + ad1.g) - (ad4.g + 2.0 * mx2 + ad3.g);
        float gy = (ad3.g + 2.0 * my1 + ad1.g) - (ad4.g + 2.0 * my2 + ad2.g);
        float edge = length(float2(gx, gy)) * 0.25;
        float edgeAmt = min(1.0, edge * TerrainMaskEdgeStrength);

        // SDF is already smooth — light averaging to reduce any quantization artifacts
        float m = lerp(m0, (mx1 + mx2 + my1 + my2) * 0.25, 0.15);
        float boundary = 1.0 - abs(m * 2.0 - 1.0);
        float outline = min(1.0, boundary * TerrainMaskBoundaryScale);
        float maskWidth = max(fwidth(m) * 1.4, 0.02);
        float maskSoft = smoothstep(0.5 - maskWidth, 0.5 + maskWidth, m);
        float edgeProfile = edgeAmt * smoothstep(0.05, 0.8, boundary);
        // Remove edge darkening; keep curvature/lift shaping only.
        color += maskSoft * edgeProfile * TerrainMaskSolidLift * terrainFactor;
        color += maskSoft * edgeProfile * outline * TerrainMaskRimLift * terrainFactor;

        // Material classes come from aux.b (0=None, 85=Dirt, 170=Stone, 255=Base).
        int2 auxMax = int2(max(1.0, WorldSize.x), max(1.0, WorldSize.y)) - int2(1, 1);
        int2 auxCell = clamp(int2(floor(worldCell)), int2(0, 0), auxMax);
        float4 aNearest = auxTex.Load(int3(auxCell, 0));
        float materialCode = aNearest.b * 255.0;
        float dirtMask = (1.0 - smoothstep(0.0, 40.0, abs(materialCode - 85.0))) * terrainFactor;
        float stoneMask = (1.0 - smoothstep(0.0, 40.0, abs(materialCode - 170.0))) * terrainFactor;
        float baseMask = (1.0 - smoothstep(0.0, 28.0, abs(materialCode - 255.0))) * terrainFactor;
        stoneMask = saturate(stoneMask + baseMask * 0.75);
        dirtMask = saturate(dirtMask * (1.0 - stoneMask * 0.6));

        // Jagged, multi-source stone-wall material pass for stone/base only.
        float materialMask = stoneMask;
        float3 lightDir = normalize(float3(LightDir.xy, max(0.35, LightDir.z)));

        // Base relief with ridged breakup.
        float2 baseP = worldCell * 0.026;
        float b0 = fbmNoise(baseP + float2(17.0, 3.0), 5);
        float bnx = fbmNoise(baseP + float2(19.0, 3.0), 5);
        float bny = fbmNoise(baseP + float2(17.0, 5.0), 5);
        float2 bGrad = float2(bnx - b0, bny - b0);
        float ridgedBase = 1.0 - abs(b0 * 2.0 - 1.0);
        float3 baseNormal = normalize(float3(-bGrad * 11.0, 1.0));
        float baseDiffuse = dot(baseNormal, lightDir) * 0.5 + 0.5;
        float baseCavity = smoothstep(0.55, 0.10, b0);
        float baseDark = (0.04 + 0.12 * baseCavity + 0.05 * ridgedBase) * materialMask;
        color *= 1.0 - baseDark;
        color = lerp(color, color * (0.76 + 0.42 * baseDiffuse), materialMask * 0.88);

        // High-frequency chipped grain.
        float2 grainP = worldCell * 0.145;
        float g0 = fbmNoise(grainP + float2(121.0, 33.0), 3);
        float gnx = fbmNoise(grainP + float2(123.0, 33.0), 3);
        float gny = fbmNoise(grainP + float2(121.0, 35.0), 3);
        float2 gGrad = float2(gnx - g0, gny - g0);
        float3 grainNormal = normalize(float3(-gGrad * 5.3, 1.0));
        float grainDiffuse = dot(grainNormal, lightDir) * 0.5 + 0.5;
        float grainMask = materialMask * 0.75;
        float grainCavity = smoothstep(0.60, 0.20, g0);
        color *= 1.0 - grainMask * (0.015 + 0.045 * grainCavity);
        color = lerp(color, color * (0.88 + 0.22 * grainDiffuse), grainMask);

        // Angular strata and fracture lines for wall-like structure.
        float2 strataP = float2(
            worldCell.x * 0.060 + worldCell.y * 0.022,
            -worldCell.x * 0.022 + worldCell.y * 0.060);
        float strataNoise = fbmNoise(strataP + float2(211.0, 87.0), 4);
        float ridge = 1.0 - abs(strataNoise * 2.0 - 1.0);
        float strata = pow(saturate(ridge), 3.2);
        float crack = smoothstep(0.80, 0.985, strata);
        color *= 1.0 - materialMask * (0.055 * strata + 0.090 * crack);

        // Block/chisel breakup from coarse cell noise.
        float2 blockP = worldCell * 0.095;
        float2 blockCell = floor(blockP);
        float2 blockLocal = frac(blockP) - 0.5;
        float blockRnd = hash21(blockCell + float2(401.0, 59.0));
        float ang = blockRnd * 6.2831853;
        float2 blockDir = normalize(float2(cos(ang), sin(ang)) + float2(1e-4, -1e-4));
        float chisel = 0.5 + dot(blockLocal, blockDir) * 1.9;
        float blockEdge = smoothstep(0.32, 0.50, max(abs(blockLocal.x), abs(blockLocal.y)));
        color *= 1.0 - materialMask * (0.020 * saturate(chisel) + 0.040 * blockEdge);

        // Macro breakup prevents broad uniform patches.
        float macro = fbmNoise(worldCell * 0.011 + float2(301.0, 133.0), 3);
        float macroShade = lerp(0.86, 1.12, macro);
        color = lerp(color, color * macroShade, materialMask * 0.45);

        // Dirt has its own soft clumpy profile (no stone fractures/chisel lines).
        if (dirtMask > 0.0)
        {
            float2 dirtP = worldCell * 0.040;
            float d0 = fbmNoise(dirtP + float2(19.0, 201.0), 4);
            float dnx = fbmNoise(dirtP + float2(21.0, 201.0), 4);
            float dny = fbmNoise(dirtP + float2(19.0, 203.0), 4);
            float2 dGrad = float2(dnx - d0, dny - d0);
            float3 dirtNormal = normalize(float3(-dGrad * 4.8, 1.0));
            float dirtDiffuse = dot(dirtNormal, lightDir) * 0.5 + 0.5;

            float clump = smoothstep(0.24, 0.74, d0);
            float pore = smoothstep(0.56, 0.18, fbmNoise(worldCell * 0.155 + float2(71.0, 13.0), 2));
            float dirtDark = 0.020 + 0.060 * pore + 0.025 * (1.0 - clump);
            color *= 1.0 - dirtMask * dirtDark;

            float3 dirtTint = lerp(float3(0.92, 0.82, 0.70), float3(1.05, 0.92, 0.78), clump);
            color = lerp(color, color * dirtTint * (0.90 + 0.18 * dirtDiffuse), dirtMask * 0.82);

            float looseDust = fbmNoise(worldCell * 0.095 + float2(151.0, 39.0), 3);
            color = lerp(color, color * lerp(0.94, 1.06, looseDust), dirtMask * 0.30);
        }

        float3 aaNeighborhood =
            sceneTex.Sample(s0, uv + float2(mTexel.x, 0.0)).rgb +
            sceneTex.Sample(s0, uv - float2(mTexel.x, 0.0)).rgb +
            sceneTex.Sample(s0, uv + float2(0.0, mTexel.y)).rgb +
            sceneTex.Sample(s0, uv - float2(0.0, mTexel.y)).rgb +
            sceneTex.Sample(s0, uv + float2(mTexel.x, mTexel.y)).rgb +
            sceneTex.Sample(s0, uv + float2(mTexel.x, -mTexel.y)).rgb +
            sceneTex.Sample(s0, uv + float2(-mTexel.x, mTexel.y)).rgb +
            sceneTex.Sample(s0, uv + float2(-mTexel.x, -mTexel.y)).rgb;
        aaNeighborhood *= (1.0 / 8.0);
        float aaMix = saturate(edgeProfile * 0.30) * terrainFactor;
        color = lerp(color, aaNeighborhood, aaMix);

        // Normal-map style terrain lighting disabled for now.

        // Local + wider blur so boosted low-temperature glow does not form hard contours.
        float heatLocal = a0.r * 0.30
            + (ax1.r + ax2.r + ay1.r + ay2.r) * 0.10
            + (ad1.r + ad2.r + ad3.r + ad4.r) * 0.05;
        float2 mTexel2 = mTexel * 2.0;
        float heatWide =
            auxTex.Sample(s0, auxUv + float2(mTexel2.x, 0.0)).r * 0.25 +
            auxTex.Sample(s0, auxUv - float2(mTexel2.x, 0.0)).r * 0.25 +
            auxTex.Sample(s0, auxUv + float2(0.0, mTexel2.y)).r * 0.25 +
            auxTex.Sample(s0, auxUv - float2(0.0, mTexel2.y)).r * 0.25;
        // Detect steep thermal transitions so we can soften hard contour rings.
        float heatGrad = abs(ax1.r - ax2.r) + abs(ay1.r - ay2.r);
        float heatEdge = saturate(heatGrad * 1.80);
        float heatBlend = lerp(0.35, 0.75, heatEdge);
        float heat = lerp(heatLocal, heatWide, heatBlend);

        // Anti-aliased fade-in: widen transition where heat gradient is steep.
        float edgeSoft = max(0.008, fwidth(heat) * (4.0 + 4.0 * heatEdge));
        float fadeIn = smoothstep(0.002 - edgeSoft, 0.09 + edgeSoft, heat);
        float heatNorm = heat * fadeIn;

        // Keep strong saturation/intensity while smoothly masking low-end onset.
        float t = pow(saturate(heatNorm), lerp(0.62, 0.86, heatEdge));
        float3 glow = HeatRamp(t);
        float heatGain = lerp(2.00, 2.35, t) * lerp(1.0, 0.92, heatEdge);
        float visible = smoothstep(0.0, lerp(0.040, 0.085, heatEdge), heatNorm);
        color += glow * heatGain * visible;

        float phase = frac(sin(dot(floor(worldCell), float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
        float pulse = MaterialEmissivePulse.y + MaterialEmissivePulse.z * (0.5 + 0.5 * sin(Time * MaterialEmissivePulse.x + phase));
        float emissive = a0.a * 0.50 + (ax1.a + ax2.a + ay1.a + ay2.a) * 0.10 + (ad1.a + ad2.a + ad3.a + ad4.a) * 0.025;
        float emissiveStrength = max(MaterialEmissiveEnergy.a, MaterialEmissiveScorched.a);
        float3 emissiveColor = lerp(MaterialEmissiveScorched.rgb, MaterialEmissiveEnergy.rgb, 0.5);
        color += emissiveColor * (emissive * emissiveStrength * pulse * terrainFactor);
    }

    float entityMaskBase = 1.0 - step(0.995, sceneTex.Sample(s0, uv).a);

    [loop]
    for (int j = 0; j < 8; j++)
    {
        if (j >= (int)TankGlowCount) break;
        float4 g = TankGlow[j];
        float heatT = saturate(g.w);
        float2 d = uv - g.xy;
        float2 dPx = d * ViewSize;
        float radiusPx = max(1e-3, g.z * max(ViewSize.x, ViewSize.y));
        float falloff = 1.0 - dot(dPx, dPx) / (radiusPx * radiusPx);
        if (falloff > 0.0)
        {
            float f2 = falloff * falloff;
            float halo = saturate(pow(saturate(falloff), 0.55) - f2 * 0.35);
            float core = pow(saturate(falloff), 0.30);
            float orangeT = smoothstep(0.86, 1.0, heatT);
            float3 tankHeatColor = lerp(float3(0.95, 0.08, 0.02), float3(1.00, 0.46, 0.06), orangeT);
            float metalHot = smoothstep(0.28, 1.0, heatT);
            float metalMask = entityMaskBase * core * (0.20 + 0.80 * metalHot);
            float baseLum = dot(color, float3(0.299, 0.587, 0.114));
            float3 steelBase = lerp(float3(0.18, 0.22, 0.28), float3(0.34, 0.38, 0.44), saturate(baseLum * 1.5));
            float3 hotMetal = lerp(steelBase, tankHeatColor, metalHot);
            hotMetal += float3(1.00, 0.92, 0.70) * pow(core, 4.0) * (0.08 + 0.34 * heatT);
            color = lerp(color, color * 0.30 + hotMetal * 1.18, saturate(metalMask));

            // Make tank body pixels self-emit when hot, not only the surrounding aura.
            float coreBoost = heatT * (0.45 + 1.15 * heatT);
            float bodyHeat = coreBoost * core * entityMaskBase;
            color += tankHeatColor * bodyHeat;

            // Animated body shimmer (stronger at mid/high heat so it is clearly visible).
            float bodyShimmer = 0.5 + 0.5 * sin(Time * 16.0 + (uv.x * 190.0 + uv.y * 160.0) + j * 1.9);
            float bodyShimmerAmp = lerp(0.10, 0.35, heatT);
            float rimShimmer = heatT * halo * (0.02 + 0.12 * bodyShimmer);
            color += tankHeatColor * (bodyHeat * (0.06 + bodyShimmerAmp * bodyShimmer) + rimShimmer);
        }
    }

    return float4(color, 1.0);
}
