void ApplyEdgeLiftPass(float2 uv, inout float3 color)
{
    if (Quality < 1.0)
        return;

    float l = dot(sceneTex.Sample(s0, uv + float2(-TexelSize.x, 0.0)).rgb, kLumaWeights);
    float r = dot(sceneTex.Sample(s0, uv + float2(TexelSize.x, 0.0)).rgb, kLumaWeights);
    float u = dot(sceneTex.Sample(s0, uv + float2(0.0, -TexelSize.y)).rgb, kLumaWeights);
    float d = dot(sceneTex.Sample(s0, uv + float2(0.0, TexelSize.y)).rgb, kLumaWeights);
    float edge = abs(r - l) + abs(d - u);
    float edgeLift = max(0.0, edge - EdgeLightBias) * EdgeLightStrength;
    color += edgeLift.xxx;
}
