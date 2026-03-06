void ApplyBloomPass(float2 uv, float3 baseColor, inout float3 color)
{
    if (Quality < 1.0)
        return;

    float2 tx = float2(TexelSize.x, 0.0);
    float2 ty = float2(0.0, TexelSize.y);
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
