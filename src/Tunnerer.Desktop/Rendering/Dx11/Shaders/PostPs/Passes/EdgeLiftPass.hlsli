void ApplyEdgeLiftPass(float2 uv, inout float3 color)
{
    if (Quality < 1.0)
        return;

    float l = dot(sceneTex.Sample(s0, uv + float2(-TexelSize.x, 0.0)).rgb, float3(0.299, 0.587, 0.114));
    float r = dot(sceneTex.Sample(s0, uv + float2(TexelSize.x, 0.0)).rgb, float3(0.299, 0.587, 0.114));
    float u = dot(sceneTex.Sample(s0, uv + float2(0.0, -TexelSize.y)).rgb, float3(0.299, 0.587, 0.114));
    float d = dot(sceneTex.Sample(s0, uv + float2(0.0, TexelSize.y)).rgb, float3(0.299, 0.587, 0.114));
    float edge = abs(r - l) + abs(d - u);
    float edgeLift = max(0.0, edge - EdgeLightBias) * EdgeLightStrength;
    color += edgeLift.xxx;
}
