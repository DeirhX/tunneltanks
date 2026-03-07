// ============================================================================
// Passes/EdgeLiftPass.hlsli — Edge-detection brightness boost
// ============================================================================
//
// Computes a simple 1-pixel luminance edge magnitude (horizontal + vertical
// Sobel-like differences) and adds brightness proportional to edge strength.
// This lifts geometric edges slightly without altering flat areas, giving
// the scene a subtle outlined look.
//
// Requires Quality >= 1 to activate.
// ============================================================================

void ApplyEdgeLiftPass(float2 uv, inout float3 color)
{
    if (Quality < 1.0)
        return;

    float2 tx = TexelOffsetX();
    float2 ty = TexelOffsetY();
    float l = dot(sceneTex.Sample(s0, uv - tx).rgb, kLumaWeights);
    float r = dot(sceneTex.Sample(s0, uv + tx).rgb, kLumaWeights);
    float u = dot(sceneTex.Sample(s0, uv - ty).rgb, kLumaWeights);
    float d = dot(sceneTex.Sample(s0, uv + ty).rgb, kLumaWeights);
    float edge = abs(r - l) + abs(d - u);
    float edgeLift = max(0.0, edge - EdgeLightBias) * EdgeLightStrength;
    color += edgeLift.xxx;
}
