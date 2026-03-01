Texture2D sourceTex : register(t0);
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

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    float2 screenPx = uv * ViewSize;
    float2 worldCell = (CameraPixels + screenPx) / max(1.0, PixelScale);

    // Nearest-neighbor fetch (avoids bilinear bleed across entity/terrain boundaries)
    int2 maxCell = int2(max(1.0, WorldSize.x), max(1.0, WorldSize.y)) - int2(1, 1);
    int2 cell = clamp(int2(floor(worldCell)), int2(0, 0), maxCell);
    float4 nearestSample = sourceTex.Load(int3(cell, 0));
    float3 nearestColor = nearestSample.rgb;

    // Entity pixels are marked with alpha < 1.0 — pass them through unmodified
    float entityFlag = nearestSample.a;
    if (entityFlag < 0.999)
        return float4(nearestColor, entityFlag);

    // Bilinear sampling of source colors using texture UV
    float2 texUv = worldCell / WorldSize;
    float3 bilinearColor = sourceTex.Sample(s0, texUv).rgb;

    // Nudge SDF sampling toward bottom-left so the visible outline shifts
    // toward top-right, covering raw pixel edges at cell boundaries.
    float2 sdfUv = texUv + float2(-0.15, 0.15) / WorldSize;
    float sdf = auxTex.Sample(s0, sdfUv).g;

    // Bias SDF slightly toward cave — expands the curve outward into solid
    // terrain in all directions, covering raw pixel edges at cell boundaries.
    sdf -= 0.025;

    // Smooth alpha from SDF — transition width adapts to edge softness
    float edgeSoftness = max(0.02, NativeContinuousParams.x);
    float alpha = smoothstep(0.5 - edgeSoftness, 0.5 + edgeSoftness, sdf);

    // Boundary-local subpixel smoothing for thin/single-cell canyons.
    // This hides staircase remnants by averaging 4 nearby SDF samples.
    float edgeBand = edgeSoftness * 1.8;
    if (abs(sdf - 0.5) < edgeBand)
    {
        float2 sub = float2(0.35, 0.35) / WorldSize;
        float s1 = auxTex.Sample(s0, sdfUv + float2(-sub.x, -sub.y)).g - 0.025;
        float s2 = auxTex.Sample(s0, sdfUv + float2( sub.x, -sub.y)).g - 0.025;
        float s3 = auxTex.Sample(s0, sdfUv + float2(-sub.x,  sub.y)).g - 0.025;
        float s4 = auxTex.Sample(s0, sdfUv + float2( sub.x,  sub.y)).g - 0.025;
        float aSub =
            smoothstep(0.5 - edgeSoftness, 0.5 + edgeSoftness, s1) +
            smoothstep(0.5 - edgeSoftness, 0.5 + edgeSoftness, s2) +
            smoothstep(0.5 - edgeSoftness, 0.5 + edgeSoftness, s3) +
            smoothstep(0.5 - edgeSoftness, 0.5 + edgeSoftness, s4);
        alpha = (alpha + aSub * 0.25) * 0.5;
    }

    // Blend bilinear (smooth at boundaries) with nearest (sharp deep inside)
    float boundaryProximity = 1.0 - abs(sdf * 2.0 - 1.0);
    float bilinearWeight = smoothstep(0.0, 0.4, boundaryProximity) * NativeContinuousParams.y;
    float3 solidColor = lerp(nearestColor, bilinearColor, saturate(bilinearWeight));

    // Cave color (very dark, not pure black to allow depth variation)
    float3 caveColor = float3(0.055, 0.055, 0.063);

    float3 color = lerp(caveColor, solidColor, alpha);
    return float4(color, 1.0);
}
