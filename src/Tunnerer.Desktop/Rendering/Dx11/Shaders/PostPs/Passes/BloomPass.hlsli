// ============================================================================
// Passes/BloomPass.hlsli — Single-pass 3x3 bloom
// ============================================================================
//
// Simple additive bloom: samples a 3x3 neighborhood of sceneTex, subtracts
// BloomThreshold from each sample (via bright()), and adds the weighted sum
// back. The kernel uses configurable axis and diagonal weights to control
// bloom spread shape.
//
// Requires Quality >= 1 to activate.
// ============================================================================

void ApplyBloomPass(float2 uv, float3 baseColor, inout float3 color)
{
    if (Quality < 1.0)
        return;

    float2 tx = TexelOffsetX();
    float2 ty = TexelOffsetY();
    float2 d1 = float2(TexelSize.x, TexelSize.y);
    float2 d2 = float2(TexelSize.x, -TexelSize.y);

    float3 bloom = bright(baseColor) * BloomWeightCenter;
    bloom += bright(sceneTex.Sample(s0, uv + tx).rgb) * BloomWeightAxis;
    bloom += bright(sceneTex.Sample(s0, uv - tx).rgb) * BloomWeightAxis;
    bloom += bright(sceneTex.Sample(s0, uv + ty).rgb) * BloomWeightAxis;
    bloom += bright(sceneTex.Sample(s0, uv - ty).rgb) * BloomWeightAxis;
    bloom += bright(sceneTex.Sample(s0, uv + d1).rgb) * BloomWeightDiagonal;
    bloom += bright(sceneTex.Sample(s0, uv - d1).rgb) * BloomWeightDiagonal;
    bloom += bright(sceneTex.Sample(s0, uv + d2).rgb) * BloomWeightDiagonal;
    bloom += bright(sceneTex.Sample(s0, uv - d2).rgb) * BloomWeightDiagonal;

    color += bloom * BloomStrength;
}
