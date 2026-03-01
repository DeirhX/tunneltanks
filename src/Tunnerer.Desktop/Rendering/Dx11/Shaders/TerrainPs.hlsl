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
    float TankGlowCount;
    float4 TankGlow[8];
};

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    float2 screenPx = uv * ViewSize;
    float2 worldCell = (CameraPixels + screenPx) / max(1.0, PixelScale);
    int2 maxCell = int2(max(1.0, WorldSize.x), max(1.0, WorldSize.y)) - int2(1, 1);
    int2 cell = clamp(int2(floor(worldCell)), int2(0, 0), maxCell);
    int2 cellX1 = clamp(cell + int2(1, 0), int2(0, 0), maxCell);
    int2 cellX2 = clamp(cell - int2(1, 0), int2(0, 0), maxCell);
    int2 cellY1 = clamp(cell + int2(0, 1), int2(0, 0), maxCell);
    int2 cellY2 = clamp(cell - int2(0, 1), int2(0, 0), maxCell);

    float3 c0 = sourceTex.Load(int3(cell, 0)).rgb;
    float3 cBlend = c0 * 0.70 +
        sourceTex.Load(int3(cellX1, 0)).rgb * 0.075 +
        sourceTex.Load(int3(cellX2, 0)).rgb * 0.075 +
        sourceTex.Load(int3(cellY1, 0)).rgb * 0.075 +
        sourceTex.Load(int3(cellY2, 0)).rgb * 0.075;

    float m0 = auxTex.Load(int3(cell, 0)).g;
    float edge = 1.0 - abs(m0 * 2.0 - 1.0);
    float edgeW = smoothstep(0.0, max(0.001, NativeContinuousParams.x), edge) * NativeContinuousParams.y;
    edgeW *= (0.15 + 0.35 * saturate(NativeContinuousParams.z));
    float3 color = lerp(c0, cBlend, saturate(edgeW));
    return float4(color, 1.0);
}
