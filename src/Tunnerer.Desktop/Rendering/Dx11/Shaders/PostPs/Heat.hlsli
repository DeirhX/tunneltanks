float3 HeatRamp(float t)
{
    t = saturate(t);
    // Non-black start so low temperatures remain visibly warm.
    float3 c0 = float3(0.14, 0.01, 0.00);
    float3 c1 = float3(0.75, 0.02, 0.00);
    float3 c2 = float3(0.95, 0.26, 0.02);
    float3 c3 = float3(1.00, 0.78, 0.10);
    float3 c4 = float3(1.00, 1.00, 0.80);

    // Smoothly blend through anchor colors to avoid visible band edges.
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

float3 HeatDebugRamp(float t)
{
    t = saturate(t);
    float3 cold = float3(0.08, 0.25, 1.00);
    float3 mid = float3(1.00, 0.92, 0.15);
    float3 hot = float3(1.00, 0.12, 0.05);
    float split = step(0.5, t);
    float3 lo = lerp(cold, mid, t * 2.0);
    float3 hi = lerp(mid, hot, (t - 0.5) * 2.0);
    return lerp(lo, hi, split);
}
