// ============================================================================
// Materials/TerrainShared.hlsli — Shared terrain sampling utilities
// ============================================================================
//
// Provides low-frequency landform sampling used by both stone and dirt
// materials to create large-scale terrain features (hills, ridges, plateaus,
// eroded cliff bands) that are consistent across material boundaries.
// ============================================================================

// Samples a broad landform height at the given world-cell position.
// Returns [0..1] — 0 = valley, 1 = hilltop. Uses two overlapping 4-octave
// and 3-octave fbmNoise passes with rotated coordinates to break up
// axis-aligned repetition.
float SampleLandformHeight(float2 worldCell)
{
    float broad = fbmNoise(worldCell * 0.0075 + float2(71.0, 911.0), 4);
    float folded = fbmNoise(float2(
        worldCell.x * 0.006 + worldCell.y * 0.002,
        -worldCell.x * 0.002 + worldCell.y * 0.006) + float2(313.0, 509.0), 3);
    return saturate(broad * 0.62 + folded * 0.58);
}
