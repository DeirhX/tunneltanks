namespace Tunnerer.Desktop.Rendering.Dx11.Shaders;

internal static class PostPs
{
    public const string Source = @"
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
    float TankGlowCount;
    float4 TankGlow[8];
};

float3 bright(float3 c) { return max(c - float3(BloomThreshold, BloomThreshold, BloomThreshold), 0.0); }

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    float3 baseColor = sceneTex.Sample(s0, uv).rgb;
    float3 color = baseColor;

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
        float2 auxUv = (worldCell + float2(0.5, 0.5)) / WorldSize;
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
        float gx = (ad2.g + 2.0 * mx1 + ad1.g) - (ad4.g + 2.0 * mx2 + ad3.g);
        float gy = (ad3.g + 2.0 * my1 + ad1.g) - (ad4.g + 2.0 * my2 + ad2.g);
        float edge = length(float2(gx, gy)) * 0.25;
        float edgeAmt = min(1.0, edge * TerrainMaskEdgeStrength * 1.2);
        float mDiag = (ad1.g + ad2.g + ad3.g + ad4.g) * 0.25;
        float m = lerp(m0, mDiag, 0.12);
        float mSmooth = (m0 * 4.0 + mx1 + mx2 + my1 + my2 + ad1.g + ad2.g + ad3.g + ad4.g) / 12.0;
        m = lerp(m, mSmooth, 0.55);
        float boundary = 1.0 - abs(m * 2.0 - 1.0);
        float outline = min(1.0, boundary * TerrainMaskBoundaryScale);
        float maskWidth = max(fwidth(m) * 1.6, 0.045);
        float maskSoft = smoothstep(0.5 - maskWidth, 0.5 + maskWidth, m);
        float edgeProfile = edgeAmt * smoothstep(0.08, 0.95, boundary);
        float energyMask = saturate(a0.b * 2.0);
        float outlineDarken = TerrainMaskOutlineDarken * (1.0 - 0.55 * energyMask);
        color *= 1.0 - outline * outlineDarken;
        color *= 1.0 - (1.0 - maskSoft) * edgeProfile * TerrainMaskCaveDarken;
        color += maskSoft * edgeProfile * TerrainMaskSolidLift;
        color += maskSoft * edgeProfile * outline * TerrainMaskRimLift;

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
        float aaMix = saturate(edgeProfile * 0.30);
        color = lerp(color, aaNeighborhood, aaMix);

        float heat = a0.r * 0.50 + (ax1.r + ax2.r + ay1.r + ay2.r) * 0.125;
        if (heat > TerrainHeatGlowColorAndThreshold.a)
        {
            float t2 = heat * heat;
            color.r += TerrainHeatGlowColorAndThreshold.r * t2;
            color.g += TerrainHeatGlowColorAndThreshold.g * t2 * heat;
            color.b += TerrainHeatGlowColorAndThreshold.b * t2 * t2;
        }

        float phase = frac(sin(dot(floor(worldCell), float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
        float pulse = MaterialEmissivePulse.y + MaterialEmissivePulse.z * (0.5 + 0.5 * sin(Time * MaterialEmissivePulse.x + phase));
        float energy = a0.b * 0.50 + (ax1.b + ax2.b + ay1.b + ay2.b) * 0.10 + (ad1.b + ad2.b + ad3.b + ad4.b) * 0.025;
        color += MaterialEmissiveEnergy.rgb * (energy * MaterialEmissiveEnergy.a * pulse);
        color += MaterialEmissiveScorched.rgb * (a0.a * MaterialEmissiveScorched.a * pulse);
    }

    [loop]
    for (int i = 0; i < 8; i++)
    {
        if (i >= (int)TankGlowCount) break;
        float4 g = TankGlow[i];
        float2 d = uv - g.xy;
        float falloff = 1.0 - dot(d, d) / max(1e-6, g.z * g.z);
        if (falloff > 0.0)
        {
            falloff *= falloff;
            color += TankHeatGlowColor.rgb * (g.w * falloff);
        }
    }

    return float4(color, 1.0);
}";
}
