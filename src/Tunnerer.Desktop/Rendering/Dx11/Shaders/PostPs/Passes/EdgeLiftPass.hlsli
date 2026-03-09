// ============================================================================
// Passes/EdgeLiftPass.hlsli — Terrain-boundary blend pass
// ============================================================================
//
// Softens terrain staircase boundaries using a terrain-only local blend.
// Unlike the previous luminance edge lift, this pass does not touch entity
// pixels (tanks/projectiles/UI silhouettes) and therefore avoids global
// contrast amplification.
//
// Requires Quality >= 1 to activate.
// ============================================================================

void ApplyEdgeLiftPass(float2 uv, float terrainFactor, inout float3 color)
{
    if (Quality < 1.0 || terrainFactor <= 0.5 || UseTerrainAux <= 0.5 || PixelScale <= 0.0)
        return;

    float2 oneCell = PixelScale * TexelSize;
    float4 c = sceneTex.Sample(s0, uv);
    float4 l = sceneTex.Sample(s0, uv - float2(oneCell.x, 0.0));
    float4 r = sceneTex.Sample(s0, uv + float2(oneCell.x, 0.0));
    float4 u = sceneTex.Sample(s0, uv - float2(0.0, oneCell.y));
    float4 d = sceneTex.Sample(s0, uv + float2(0.0, oneCell.y));

    // Terrain-only weighted average. Entity neighbors contribute zero weight.
    float wC = step(kTerrainAlphaThreshold, c.a) * 2.0;
    float wL = step(kTerrainAlphaThreshold, l.a);
    float wR = step(kTerrainAlphaThreshold, r.a);
    float wU = step(kTerrainAlphaThreshold, u.a);
    float wD = step(kTerrainAlphaThreshold, d.a);
    float wSum = wC + wL + wR + wU + wD;
    if (wSum <= 0.001)
        return;

    float3 terrainAverage = (c.rgb * wC + l.rgb * wL + r.rgb * wR + u.rgb * wU + d.rgb * wD) / wSum;

    // Blend is driven by cell-scale color edges plus SDF boundary proximity.
    float colorEdge = abs(dot(r.rgb - l.rgb, kLumaWeights)) + abs(dot(d.rgb - u.rgb, kLumaWeights));
    float2 screenPx = uv * ViewSize;
    float2 worldCell = (CameraPixels + screenPx) / max(1.0, PixelScale);
    float2 auxUv = worldCell / WorldSize;
    float sdf = auxTex.Sample(s0, auxUv).g;
    float boundary = 1.0 - abs(sdf * 2.0 - 1.0);

    float edgeSignal = max(smoothstep(0.01, 0.10, colorEdge), smoothstep(0.02, 0.35, boundary));
    float blend = saturate(max(0.0, edgeSignal - EdgeLightBias) * (0.20 + EdgeLightStrength * 0.50));
    color = lerp(color, terrainAverage, blend * terrainFactor);
}
