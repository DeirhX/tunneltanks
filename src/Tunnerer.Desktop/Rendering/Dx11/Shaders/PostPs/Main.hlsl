// ============================================================================
// PostPs/Main.hlsl — Full-screen post-processing pixel shader
// ============================================================================
//
// Entry point for the post-processing pass. Runs once per screen pixel after
// the terrain (TerrainPs) and entities have been composited into sceneTex.
//
// Pipeline order:
//   1. Heat haze distortion (ComputeTankHeatHaze + ApplyOutlineHeatDistortion)
//   2. Bloom (ApplyBloomPass)
//   3. Vignette (ApplyVignettePass)
//   4. Edge lift (ApplyEdgeLiftPass)
//   5. Terrain curve smoothing (ApplyTerrainCurvePass)
//   6. Terrain aux: SDF shading, material overlays, heat glow (ApplyTerrainAuxPass)
//   7. Tank heat glow (ApplyTankHeatGlowPass)
//
// Inputs:
//   sceneTex (t0) — composited scene (terrain + entities)
//   auxTex   (t1) — per-cell terrain data (.r = heat, .g = SDF, .b = material ID, .a = scorch)
//
// Terrain vs entity discrimination:
//   sceneSample.a >= 1.0 → terrain pixel   (terrainFactor = 1)
//   sceneSample.a <  1.0 → entity pixel    (terrainFactor = 0)
//   This prevents terrain-only effects from bleeding onto tanks/projectiles.
// ============================================================================

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
#include "Passes/TerrainCurvePass.hlsli"
#include "Passes/TerrainAuxPass.hlsli"
#include "Passes/TankGlowPass.hlsli"

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    // ---- Heat haze UV distortion ------------------------------------------
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

    // ---- Post-process chain -----------------------------------------------
    ApplyBloomPass(uv, baseColor, color);
    ApplyVignettePass(uv, color);
    ApplyEdgeLiftPass(uv, color);
    ApplyTerrainCurvePass(uv, terrainFactor, color);
    ApplyTerrainAuxPass(uv, terrainFactor, color);
    ApplyTankHeatGlowPass(uv, color);

    return float4(color, 1.0);
}
