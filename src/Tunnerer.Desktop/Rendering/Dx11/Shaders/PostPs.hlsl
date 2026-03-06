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
    float HeatDebugOverlay;
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
#include "PostPs/Noise.hlsli"
#include "PostPs/Heat.hlsli"
#include "PostPs/Materials.hlsli"

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

        // Material classes come from aux.b (0=None, 85=Dirt, 170=Stone, 212=Energy, 255=Base).
        int2 auxMax = int2(max(1.0, WorldSize.x), max(1.0, WorldSize.y)) - int2(1, 1);
        int2 auxCell = clamp(int2(floor(worldCell)), int2(0, 0), auxMax);
        float4 aNearest = auxTex.Load(int3(auxCell, 0));
        float materialCode = aNearest.b * 255.0;
        float dirtMask = (1.0 - smoothstep(0.0, 40.0, abs(materialCode - 85.0))) * terrainFactor;
        float stoneMask = (1.0 - smoothstep(0.0, 40.0, abs(materialCode - 170.0))) * terrainFactor;
        float energyMask = (1.0 - smoothstep(0.0, 28.0, abs(materialCode - 212.0))) * terrainFactor;
        float baseMask = (1.0 - smoothstep(0.0, 28.0, abs(materialCode - 255.0))) * terrainFactor;
        stoneMask = saturate(stoneMask + baseMask * 0.75);
        dirtMask = saturate(dirtMask * (1.0 - stoneMask * 0.6) * (1.0 - energyMask));

        float3 lightDir = normalize(float3(LightDir.xy, max(0.35, LightDir.z)));
        // Material shading is split into helper functions to keep this pass manageable.
        ApplyStoneMaterial(color, worldCell, stoneMask, lightDir);
        ApplyDirtMaterial(color, worldCell, dirtMask, lightDir);

        // Energy material profile: smoother crystalline body + pulsing emissive veins.
        if (energyMask > 0.0)
        {
            float2 eP = worldCell * 0.060;
            float e0 = fbmNoise(eP + float2(7.0, 241.0), 4);
            float enx = fbmNoise(eP + float2(9.0, 241.0), 4);
            float eny = fbmNoise(eP + float2(7.0, 243.0), 4);
            float2 eGrad = float2(enx - e0, eny - e0);
            float3 eNormal = normalize(float3(-eGrad * 6.0, 1.0));
            float eDiffuse = dot(eNormal, lightDir) * 0.5 + 0.5;

            float veins = smoothstep(0.76, 0.95, 1.0 - abs(e0 * 2.0 - 1.0));
            float pulseE = MaterialEmissivePulse.y + MaterialEmissivePulse.z * (0.5 + 0.5 * sin(Time * MaterialEmissivePulse.x + e0 * 9.0));
            float3 energyTint = lerp(float3(0.88, 0.90, 0.66), float3(1.00, 1.00, 0.78), eDiffuse);

            color = lerp(color, color * energyTint * (0.94 + 0.14 * eDiffuse), energyMask * 0.85);
            color += MaterialEmissiveEnergy.rgb * (veins * MaterialEmissiveEnergy.a * pulseE * energyMask);
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
        float scorch = a0.a * 0.50 + (ax1.a + ax2.a + ay1.a + ay2.a) * 0.10 + (ad1.a + ad2.a + ad3.a + ad4.a) * 0.025;
        color += MaterialEmissiveScorched.rgb * (scorch * MaterialEmissiveScorched.a * pulse * terrainFactor);

        // Heat debug overlay: half-transparent temperature map (blue->yellow->red).
        if (HeatDebugOverlay > 0.5)
        {
            float temperature = a0.r * 255.0 * 4.0;
            float heat01 = saturate(temperature / 100.0);
            color = lerp(color, HeatDebugRamp(heat01), 0.5);
        }
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
