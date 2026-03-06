void ApplyVignettePass(float2 uv, inout float3 color)
{
    if (Quality < 2.0)
        return;

    float d = distance(uv, float2(0.5, 0.5));
    float vig = 1.0 - smoothstep(VignetteInnerRadius, VignetteOuterRadius, d) * VignetteStrength;
    color *= vig;
}
