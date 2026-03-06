#include "Common/Bindings.hlsli"
#include "Common/Constants.hlsli"

#include "Common/MathNoise.hlsli"
#include "Common/ColorRamps.hlsli"

#include "Materials/TerrainShared.hlsli"
#include "Materials/StoneMaterial.hlsli"
#include "Materials/DirtMaterial.hlsli"
#include "Materials/EnergyMaterial.hlsli"

#include "Passes/DistortionPass.hlsli"
#include "Passes/BloomPass.hlsli"
#include "Passes/VignettePass.hlsli"
#include "Passes/EdgeLiftPass.hlsli"
#include "Passes/TerrainAuxPass.hlsli"
#include "Passes/TankGlowPass.hlsli"

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    float2 hazeOffset;
    float outlineHeatMask;
    float distortionEnabled;
    ComputeTankHeatHaze(uv, hazeOffset, outlineHeatMask, distortionEnabled);
    ApplyOutlineHeatDistortion(uv, distortionEnabled, outlineHeatMask, hazeOffset);

    float2 hazeUv = clamp(uv + hazeOffset, TexelSize * 0.5, 1.0 - TexelSize * 0.5);
    float4 sceneSample = sceneTex.Sample(s0, hazeUv);
    float3 baseColor = sceneSample.rgb;
    float3 color = baseColor;
    float terrainFactor = step(0.999, sceneSample.a);

    ApplyBloomPass(uv, baseColor, color);
    ApplyVignettePass(uv, color);
    ApplyEdgeLiftPass(uv, color);
    ApplyTerrainAuxPass(uv, terrainFactor, color);
    ApplyTankHeatGlowPass(uv, color);

    return float4(color, 1.0);
}
