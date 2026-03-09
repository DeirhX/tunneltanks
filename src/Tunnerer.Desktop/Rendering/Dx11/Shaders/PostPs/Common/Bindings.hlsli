// ============================================================================
// Common/Bindings.hlsli — Texture, sampler, and cbuffer declarations
// ============================================================================
//
// Shared by all post-processing passes. Declares the two input textures,
// the bilinear sampler, and includes the shared PostParams constant buffer.
// ============================================================================

Texture2D sceneTex : register(t0);  // Composited scene (TerrainPs output + entities)
Texture2D auxTex : register(t1);    // Per-cell terrain aux (.r=heat, .g=SDF, .b=materialID, .a=scorch)
SamplerState s0 : register(s0);     // Bilinear-clamp sampler

#include "PostParams.hlsli"
