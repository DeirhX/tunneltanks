// ============================================================================
// Common/ColorRamps.hlsli — Temperature-to-color mapping functions
// ============================================================================
//
// Provides two color ramps for converting scalar heat values to RGB:
//
//   HeatRamp      — Gameplay heat glow: dark red → bright red → orange →
//                   yellow → white-hot. Used by TerrainAuxPass for terrain
//                   heat emissive.
//
//   HeatDebugRamp — Debug overlay: blue (cold) → yellow (mid) → red (hot).
//                   Activated by HeatDebugOverlay cbuffer flag.
//
// Both functions accept a [0..1] parameter and return an RGB color.
// ============================================================================

// Gameplay heat ramp: 5-stop gradient from dim ember to white-hot.
float3 HeatRamp(float t)
{
    t = saturate(t);
    float3 c0 = float3(0.14, 0.01, 0.00);  // dim ember
    float3 c1 = float3(0.75, 0.02, 0.00);  // deep red
    float3 c2 = float3(0.95, 0.26, 0.02);  // orange-red
    float3 c3 = float3(1.00, 0.78, 0.10);  // bright orange-yellow
    float3 c4 = float3(1.00, 1.00, 0.80);  // white-hot

    float w1 = smoothstep(0.16, 0.42, t);
    float w2 = smoothstep(0.38, 0.66, t);
    float w3 = smoothstep(0.68, 0.92, t);
    float w4 = smoothstep(0.90, 1.00, t);

    float3 col = lerp(c0, c1, w1);
    col = lerp(col, c2, w2);
    col = lerp(col, c3, w3);
    col = lerp(col, c4, w4);
    return col;
}

// Debug temperature overlay: blue → yellow → red, with hard midpoint split.
float3 HeatDebugRamp(float t)
{
    t = saturate(t);
    float3 cold = float3(0.08, 0.25, 1.00);   // blue
    float3 mid  = float3(1.00, 0.92, 0.15);   // yellow
    float3 hot  = float3(1.00, 0.12, 0.05);   // red
    float split = step(0.5, t);
    float3 lo = lerp(cold, mid, t * 2.0);
    float3 hi = lerp(mid, hot, (t - 0.5) * 2.0);
    return lerp(lo, hi, split);
}
