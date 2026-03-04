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

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    // Tank heat haze: distort scene lookup around hot tanks before post-lighting.
    float2 hazeOffset = float2(0.0, 0.0);
    [loop]
    for (int i = 0; i < 8; i++)
    {
        if (i >= (int)TankGlowCount) break;
        float4 g = TankGlow[i];
        float2 d = uv - g.xy;
        float dist2 = dot(d, d);
        float radius2 = max(1e-6, g.z * g.z);
        float falloff = saturate(1.0 - dist2 / radius2);
        if (falloff <= 0.0) continue;

        float2 swirlDir = normalize(float2(-d.y, d.x) + float2(1e-4, -1e-4));
        float shimmer = sin(Time * 14.0 + (uv.x * 210.0 + uv.y * 170.0) + i * 1.7);
        float hazeStrength = g.w * falloff * falloff * 0.0180;
        hazeOffset += swirlDir * shimmer * hazeStrength;
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
        float energyMask = saturate(a0.b * 2.0);
        float outlineDarken = TerrainMaskOutlineDarken * (1.0 - 0.55 * energyMask);
        color *= 1.0 - outline * outlineDarken * terrainFactor;
        color *= 1.0 - (1.0 - maskSoft) * edgeProfile * TerrainMaskCaveDarken * terrainFactor;
        color += maskSoft * edgeProfile * TerrainMaskSolidLift * terrainFactor;
        color += maskSoft * edgeProfile * outline * TerrainMaskRimLift * terrainFactor;

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

        // --- Directional lighting from SDF-derived normals ---
        if (Quality >= 1.0 && LightDir.w > 0.0)
        {
            float nStr = LightDir.w;
            float3 normal = normalize(float3(-gx * nStr, -gy * nStr, 1.0));

            // Procedural micro-normal perturbation for surface texture
            float microStr = HalfVector.w;
            if (microStr > 0.0)
            {
                float2 noiseP = worldCell * 0.08;
                float mnx = (fbmNoise(noiseP, 3) - 0.5) * 2.0 * microStr;
                float mny = (fbmNoise(noiseP + float2(97.0, 131.0), 3) - 0.5) * 2.0 * microStr;
                normal = normalize(float3(normal.xy + float2(mnx, mny), normal.z));
            }

            // Half-Lambert diffuse
            float NdotL = dot(normal, LightDir.xyz);
            float diffuse = max(0.0, NdotL) * 0.6 + 0.4;

            // Blinn-Phong specular
            float NdotH = max(0.0, dot(normal, HalfVector.xyz));
            float spec = pow(NdotH, LightParams.z) * LightParams.w;

            float lit = LightParams.x + LightParams.y * diffuse;

            // Apply lighting only to solid terrain, cave stays unlit
            float lightMix = maskSoft * saturate(edgeProfile * 3.0 + maskSoft) * terrainFactor;
            color = lerp(color, color * lit + spec, lightMix);
        }

        // Wider spatial blur: center 0.30, cardinal 0.10, diagonal 0.05 for smooth falloff
        float heat = a0.r * 0.30
            + (ax1.r + ax2.r + ay1.r + ay2.r) * 0.10
            + (ad1.r + ad2.r + ad3.r + ad4.r) * 0.05;

        // Soft fade-in starting at ~30 degrees (byte ~7.5, norm ~0.03).
        // smoothstep gives a gentle ramp instead of a hard threshold cutoff.
        float fadeIn = smoothstep(0.02, 0.06, heat);
        float heatNorm = heat * fadeIn;
        if (heatNorm > 0.001)
        {
            // sqrt remapping for perceptual spread across the 0-1020 range.
            // Purely additive -- no opacity, just light on top of the scene.
            float t = sqrt(heatNorm);
            // Build red, orange, and white targets then blend between them with
            // overlap windows to avoid hard contour rings at threshold boundaries.
            float sRed = saturate(t / 0.32);
            float3 redGlow = float3(0.75 * sRed, 0.0, 0.0);

            float sOrange = saturate((t - 0.32) / 0.18);
            float3 orangeGlow = float3(0.75 + 0.25 * sOrange, 0.20 + 0.45 * sOrange, 0.06 + 0.10 * sOrange);

            float sWhite = saturate((t - 0.50) / 0.50);
            float3 whiteGlow = lerp(float3(1.00, 0.80, 0.25), float3(1.00, 1.00, 1.00), sWhite);

            float redToOrange = smoothstep(0.26, 0.38, t);
            float orangeToWhite = smoothstep(0.46, 0.56, t);
            float3 glow = lerp(redGlow, orangeGlow, redToOrange);
            glow = lerp(glow, whiteGlow, orangeToWhite);
            // Boost low-mid heat visibility so ~100C reads as clearly hot.
            float lowHeatBoost = lerp(1.45, 1.0, smoothstep(0.30, 0.65, t));
            color += glow * lowHeatBoost;
        }

        float phase = frac(sin(dot(floor(worldCell), float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
        float pulse = MaterialEmissivePulse.y + MaterialEmissivePulse.z * (0.5 + 0.5 * sin(Time * MaterialEmissivePulse.x + phase));
        float energy = a0.b * 0.50 + (ax1.b + ax2.b + ay1.b + ay2.b) * 0.10 + (ad1.b + ad2.b + ad3.b + ad4.b) * 0.025;
        color += MaterialEmissiveEnergy.rgb * (energy * MaterialEmissiveEnergy.a * pulse * terrainFactor);
        color += MaterialEmissiveScorched.rgb * (a0.a * MaterialEmissiveScorched.a * pulse * terrainFactor);
    }

    [loop]
    for (int j = 0; j < 8; j++)
    {
        if (j >= (int)TankGlowCount) break;
        float4 g = TankGlow[j];
        float2 d = uv - g.xy;
        float falloff = 1.0 - dot(d, d) / max(1e-6, g.z * g.z);
        if (falloff > 0.0)
        {
            float f2 = falloff * falloff;
            float halo = saturate(pow(saturate(falloff), 0.55) - f2 * 0.35);
            float glow = g.w * (f2 + 1.20 * halo);
            color += TankHeatGlowColor.rgb * glow;

            // Make the tank sprite itself radiate when hot, not only the surrounding aura.
            float entityMask = 1.0 - step(0.999, sceneTex.Sample(s0, uv).a);
            float core = pow(saturate(falloff), 0.38);
            color += TankHeatGlowColor.rgb * (g.w * core * 2.0 * entityMask);
        }
    }

    return float4(color, 1.0);
}
