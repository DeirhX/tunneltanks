// ============================================================================
// TerrainCurvePass.hlsli — Post-process boundary smoothing
// ============================================================================
//
// Runs after the terrain has been rendered to sceneTex by TerrainPs.hlsl.
// Applies a directional blur along terrain boundaries to visually soften the
// per-cell staircase produced by the discrete cell grid.
//
// Two boundary types are detected and handled with different blur kernels:
//
//   1. SDF boundaries (cave ↔ solid)
//      The SDF stored in auxTex.g transitions through 0.5 at these edges.
//      A tangent-heavy blur slides color along the boundary to round the
//      staircase corners without smearing the cave/solid transition itself.
//
//   2. Material boundaries (stone ↔ dirt, dirt ↔ energy, etc.)
//      Both sides are "solid" in the SDF, so the SDF is flat and provides
//      no gradient. Instead, we detect these edges via local color contrast
//      in sceneTex. A balanced normal+tangent blur creates a smooth gradient
//      across the pixel step.
//
// This pass preserves post-process effects (heat glow, bloom) by computing
// the difference between the full post-processed color and the raw scene
// color, blurring only the raw scene, then re-adding the effects delta.
//
// Dependencies:
//   - sceneTex (t0) — post-process source (TerrainPs output + distortion)
//   - auxTex   (t1) — auxiliary terrain data (.g = SDF, .b = material ID)
//   - PostParams cbuffer — TexelSize, ViewSize, CameraPixels, PixelScale, etc.
// ============================================================================

void ApplyTerrainCurvePass(float2 uv, float terrainFactor, inout float3 color)
{
    // Skip when terrain aux is disabled or pixel scale is zero.
    if (UseTerrainAux <= 0.5 || PixelScale <= 0.0)
        return;

    // ---- Coordinate setup -------------------------------------------------
    float2 screenPx = uv * ViewSize;
    float2 worldCell = (CameraPixels + screenPx) / max(1.0, PixelScale);
    float2 auxUv = worldCell / WorldSize;          // UV into world-space textures
    float2 mTexel = 1.0 / WorldSize;               // one cell in UV space
    float2 oneCell = PixelScale * TexelSize;        // one cell in screen UV space

    // ---- SDF boundary proximity -------------------------------------------
    // sdfBoundary is 0 deep inside cave or solid, peaks at 1 on the 0.5 isoline.
    float sdf = auxTex.Sample(s0, auxUv).g;
    float sdfBoundary = 1.0 - abs(sdf * 2.0 - 1.0);

    // ---- Color-edge detection ---------------------------------------------
    // Detect material boundaries (stone-dirt, etc.) that lack SDF transitions
    // by measuring color contrast in a 1-cell cross pattern.
    float3 sceneCenter = sceneTex.Sample(s0, uv).rgb;
    float3 scL = sceneTex.Sample(s0, uv - float2(oneCell.x, 0)).rgb;
    float3 scR = sceneTex.Sample(s0, uv + float2(oneCell.x, 0)).rgb;
    float3 scU = sceneTex.Sample(s0, uv - float2(0, oneCell.y)).rgb;
    float3 scD = sceneTex.Sample(s0, uv + float2(0, oneCell.y)).rgb;
    float edgeH = length(scR - scL);
    float edgeV = length(scD - scU);
    float colorEdge = max(edgeH, edgeV);

    // Early-out: not near any kind of boundary.
    if (sdfBoundary < 0.02 && colorEdge < 0.06)
        return;

    // ---- Boundary gradient ------------------------------------------------
    // Determines the local normal and tangent directions along the boundary.
    // For SDF boundaries: use the SDF gradient (most accurate).
    // For material boundaries (flat SDF): fall back to luminance gradient
    // derived from the color-edge samples.
    float2 grad;
    if (sdfBoundary > 0.10)
    {
        // SDF gradient via 3-cell central difference.
        grad.x = auxTex.Sample(s0, auxUv + float2(mTexel.x * 1.5, 0)).g
               - auxTex.Sample(s0, auxUv - float2(mTexel.x * 1.5, 0)).g;
        grad.y = auxTex.Sample(s0, auxUv + float2(0, mTexel.y * 1.5)).g
               - auxTex.Sample(s0, auxUv - float2(0, mTexel.y * 1.5)).g;
    }
    else
    {
        // Luminance gradient from scene color differences.
        grad.x = dot(scR - scL, kLumaWeights);
        grad.y = dot(scD - scU, kLumaWeights);
    }

    float gradLen = length(grad);
    if (gradLen < 0.001)
        return;

    // Normal points across the boundary; tangent runs along it.
    float2 normal = grad / gradLen;
    float2 tangent = float2(-normal.y, normal.x);
    float2 nUV = normal * oneCell;                 // 1-cell step in normal dir
    float2 tUV = tangent * oneCell;                // 1-cell step in tangent dir

    // ---- Preserve post-process effects ------------------------------------
    // Capture the difference between the full post-processed color and the
    // raw scene sample so bloom, heat glow, etc. survive the blur.
    float3 effectsDelta = color - sceneCenter;

    // ---- Classify boundary type -------------------------------------------
    // Material edges show high color contrast but low SDF variation.
    bool isMaterialEdge = colorEdge > sdfBoundary * 3.0 && sdfBoundary < 0.10;

    // ---- Directional blur -------------------------------------------------
    // Two kernels are produced:
    //  - `blurredWide`: existing broad curve blur
    //  - `blurredTight`: near-edge micro blur used as subtle layered contour
    float3 blurredWide;
    float3 blurredTight;

    if (isMaterialEdge)
    {
        // Material-edge kernel: balanced normal + tangent blur.
        // Spreads color across the pixel step (normal) while also smoothing
        // along the boundary (tangent) to eliminate staircase corners.
        // Kernel shape: 9 taps, ±1.0 / ±2.2 cells normal, ±2.0 / ±4.0 tangent.
        blurredWide = (
            sceneCenter * 2.5 +
            sceneTex.Sample(s0, uv + nUV * 1.0).rgb * 2.0 +
            sceneTex.Sample(s0, uv - nUV * 1.0).rgb * 2.0 +
            sceneTex.Sample(s0, uv + nUV * 1.6).rgb * 0.9 +
            sceneTex.Sample(s0, uv - nUV * 1.6).rgb * 0.9 +
            sceneTex.Sample(s0, uv + tUV * 1.6).rgb * 1.3 +
            sceneTex.Sample(s0, uv - tUV * 1.6).rgb * 1.3 +
            sceneTex.Sample(s0, uv + tUV * 2.8).rgb * 0.45 +
            sceneTex.Sample(s0, uv - tUV * 2.8).rgb * 0.45
        ) / 12.7;

        // Tighter contour layer: smaller radius and lower contrast drift.
        blurredTight = (
            sceneCenter * 3.0 +
            sceneTex.Sample(s0, uv + nUV * 0.6).rgb * 1.2 +
            sceneTex.Sample(s0, uv - nUV * 0.6).rgb * 1.2 +
            sceneTex.Sample(s0, uv + tUV * 1.0).rgb * 1.5 +
            sceneTex.Sample(s0, uv - tUV * 1.0).rgb * 1.5 +
            sceneTex.Sample(s0, uv + tUV * 2.0).rgb * 0.8 +
            sceneTex.Sample(s0, uv - tUV * 2.0).rgb * 0.8
        ) / 10.0;
    }
    else
    {
        // SDF-boundary kernel: tangent-heavy blur.
        // Rounds the cave/solid staircase by sliding color along the boundary.
        // Keeps the transition across the boundary crisp with only mild normal
        // samples to avoid fringing.
        // Kernel shape: 9 taps, ±1.5 / ±3.0 / ±4.5 cells tangent, ±1.2 normal.
        blurredWide = (
            sceneCenter * 3.0 +
            sceneTex.Sample(s0, uv + tUV * 1.5).rgb * 2.0 +
            sceneTex.Sample(s0, uv - tUV * 1.5).rgb * 2.0 +
            sceneTex.Sample(s0, uv + tUV * 2.4).rgb * 1.0 +
            sceneTex.Sample(s0, uv - tUV * 2.4).rgb * 1.0 +
            sceneTex.Sample(s0, uv + tUV * 3.2).rgb * 0.35 +
            sceneTex.Sample(s0, uv - tUV * 3.2).rgb * 0.35 +
            sceneTex.Sample(s0, uv + nUV * 1.0).rgb * 0.9 +
            sceneTex.Sample(s0, uv - nUV * 1.0).rgb * 0.9
        ) / 12.4;

        // Tighter contour layer for cave/solid edges to keep tiny formations
        // readable while still adding edge curvature.
        blurredTight = (
            sceneCenter * 3.4 +
            sceneTex.Sample(s0, uv + tUV * 0.9).rgb * 1.8 +
            sceneTex.Sample(s0, uv - tUV * 0.9).rgb * 1.8 +
            sceneTex.Sample(s0, uv + tUV * 1.8).rgb * 1.0 +
            sceneTex.Sample(s0, uv - tUV * 1.8).rgb * 1.0 +
            sceneTex.Sample(s0, uv + nUV * 0.8).rgb * 0.9 +
            sceneTex.Sample(s0, uv - nUV * 0.8).rgb * 0.9
        ) / 10.8;
    }

    // Re-apply post-process effects on top of the blurred terrain color.
    float3 curvedWide = blurredWide + effectsDelta;
    float3 curvedTight = blurredTight + effectsDelta;

    // ---- Blend strength ---------------------------------------------------
    // Ramp up from zero near the boundary threshold to full strength at the
    // boundary center. The max of both signals ensures both boundary types
    // contribute independently.
    float sdfStrength = smoothstep(0.02, 0.20, sdfBoundary);
    float colorStrength = smoothstep(0.06, 0.20, colorEdge);
    float boundarySignal = max(sdfStrength, colorStrength);
    float sdfDist = abs(sdf - 0.5);
    float edgeBandWide = 1.0 - smoothstep(0.05, 0.20, sdfDist);
    float edgeBandTight = 1.0 - smoothstep(0.03, 0.13, sdfDist);
    float materialBand = smoothstep(0.08, 0.45, colorStrength) * (1.0 - edgeBandWide * 0.75);

    // Layer 1: lower-amplitude wide blur so small dirt islands stay visible.
    float wideSignal = max(sdfStrength * edgeBandWide, materialBand * 0.45);
    float wideStrength = wideSignal * terrainFactor * 0.34;
    color = lerp(color, curvedWide, wideStrength);

    // Layer 2: tighter edge-following contour, darker and more transparent.
    float tightSignal = max(sdfStrength * edgeBandTight, materialBand * 0.30);
    float tightStrength = smoothstep(0.18, 0.80, tightSignal) * terrainFactor * 0.16;
    float3 tightLayerColor = curvedTight * 0.975;
    color = lerp(color, tightLayerColor, tightStrength);

    // Layer 3: very subtle near-edge micro contour to deepen curvature.
    float microStrength = smoothstep(0.45, 0.98, sdfStrength * edgeBandTight) * terrainFactor * 0.07;
    float3 microLayerColor = lerp(curvedTight, color, 0.35) * 0.965;
    color = lerp(color, microLayerColor, microStrength);
}
