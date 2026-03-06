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

float3 bright(float3 c)
{
    return max(c - float3(BloomThreshold, BloomThreshold, BloomThreshold), 0.0);
}
