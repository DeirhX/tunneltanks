// ============================================================================
// Common/Constants.hlsli — Shared compile-time constants
// ============================================================================
//
// Numeric constants used across multiple post-processing passes. Grouped by
// the subsystem they belong to. Keeping them here avoids magic numbers in
// pass code and makes tuning easier.
// ============================================================================

// ---- Perceptual luminance weights (Rec. 601) ------------------------------
static const float3 kLumaWeights = float3(0.299, 0.587, 0.114);
static const float kTerrainAlphaThreshold = 0.999;

// ---- Epsilon helpers ------------------------------------------------------
static const float2 kSignedEpsilon2 = float2(1e-4, -1e-4);
static const float kRadiusEpsilonPx = 1e-3;

float2 TexelOffsetX()
{
    return float2(TexelSize.x, 0.0);
}

float2 TexelOffsetY()
{
    return float2(0.0, TexelSize.y);
}

// ---- Terrain material ID encoding (auxTex.b channel) ----------------------
// CPU writes material IDs as normalized [0..1]; decode by multiplying by 255.
static const float kMaterialCodeScale = 255.0;
static const float kMaterialCodeDirt = 85.0;
static const float kMaterialCodeStone = 170.0;
static const float kMaterialCodeEnergy = 212.0;
static const float kMaterialCodeBase = 255.0;
static const float kMaterialMaskWidthWide = 40.0;    // smoothstep width for stone/dirt
static const float kMaterialMaskWidthNarrow = 28.0;  // smoothstep width for energy/base

// ---- Heat debug overlay ---------------------------------------------------
static const float kHeatDebugTemperatureScale = 255.0 * 4.0;
