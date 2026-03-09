// ============================================================================
// TerrainPs.hlsl — Terrain pixel shader (native-resolution continuous render)
// ============================================================================
//
// Renders the terrain world-map into the scene render target at 1:1 screen
// resolution. Each terrain cell is PixelScale screen-pixels wide (typically 6).
//
// Two sampling strategies are blended:
//   • Nearest-neighbor — preserves sharp per-cell texture detail deep inside
//     solid terrain.
//   • Bilinear — smooths color transitions near cave/solid and material
//     boundaries so the 6-pixel cell grid is less visible.
//
// A Gaussian-blurred SDF (stored in auxTex.g) drives alpha-blending between
// "cave" (dark empty space) and "solid" (dirt / stone / energy) terrain.
// The SDF's 0.5 isoline is further smoothed along its tangent direction to
// reduce the axis-aligned staircase inherent in cell-grid boundaries.
//
// Entity pixels (tanks, bases, projectiles) are passed through unmodified;
// they are identified by alpha < 1.0 in the source texture.
// ============================================================================

Texture2D sourceTex : register(t0);
Texture2D auxTex : register(t1);
SamplerState s0 : register(s0);

#include "PostPs/Common/PostParams.hlsli"

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    bool sharpMode = NativeContinuousParams.x <= 1e-5 && NativeContinuousParams.y <= 1e-5;
    bool nativeCurvingEnabled = NativeContinuousParams.w > 0.5;

    // ---- Screen → World coordinate mapping --------------------------------
    float2 screenPx = uv * ViewSize;
    float2 worldCell = (CameraPixels + screenPx) / max(1.0, PixelScale);

    // ---- Nearest-neighbor fetch -------------------------------------------
    // Avoids bilinear bleed across entity / terrain type boundaries.
    int2 maxCell = int2(max(1.0, WorldSize.x), max(1.0, WorldSize.y)) - int2(1, 1);
    int2 cell = clamp(int2(floor(worldCell)), int2(0, 0), maxCell);
    float4 nearestSample = sourceTex.Load(int3(cell, 0));
    float3 nearestColor = nearestSample.rgb;
    float sdfNearest = auxTex.Load(int3(cell, 0)).g;

    // ---- Entity early-out -------------------------------------------------
    // Entity pixels (tanks, projectiles, bases) carry alpha < 1.0.
    float entityFlag = nearestSample.a;
    if (entityFlag < 0.999)
        return float4(nearestColor, entityFlag);

    // Fail-safe against CPU/HLSL cbuffer drift: keep terrain unfiltered if
    // layout version does not match the shader expectation.
    if (abs(PostParamsLayoutVersion - kPostParamsLayoutVersion) > 0.001)
        return float4(nearestColor, 1.0);

    // ---- Bilinear fetch ---------------------------------------------------
    float2 texUv = worldCell / WorldSize;
    float3 bilinearColor = sourceTex.Sample(s0, texUv).rgb;

    // ---- SDF fetch & gradient ---------------------------------------------
    // auxTex.g holds a Gaussian-blurred signed-distance field:
    //   0.0 = deep cave, 0.5 = boundary, 1.0 = deep solid.
    float2 sdfUv = texUv;
    float sdf = nativeCurvingEnabled ? auxTex.Sample(s0, sdfUv).g : sdfNearest;
    float2 cellPx = 1.0 / WorldSize;

    // 3-cell central difference for a stable SDF gradient.
    float2 gradStep = cellPx * 1.5;
    float2 sdfGrad;
    sdfGrad.x = auxTex.Sample(s0, sdfUv + float2(gradStep.x, 0)).g
              - auxTex.Sample(s0, sdfUv - float2(gradStep.x, 0)).g;
    sdfGrad.y = auxTex.Sample(s0, sdfUv + float2(0, gradStep.y)).g
              - auxTex.Sample(s0, sdfUv - float2(0, gradStep.y)).g;

    // ---- Tangent-aligned SDF smoothing ------------------------------------
    // Average the SDF at 6 points along the boundary tangent (±1.25, ±2.5,
    // ±3.5 cells). This merges consecutive staircase steps, shifting the
    // 0.5 isoline from axis-aligned zigzag toward the true diagonal.
    float gradLen = length(sdfGrad);
    if (!sharpMode && nativeCurvingEnabled && gradLen > 0.001)
    {
        float2 tangent = float2(-sdfGrad.y, sdfGrad.x) / gradLen;
        float2 t1 = tangent * cellPx * 3.5;
        float2 t2 = tangent * cellPx * 2.5;
        float2 t3 = tangent * cellPx * 1.25;
        float st1 = auxTex.Sample(s0, sdfUv + t1).g;
        float st2 = auxTex.Sample(s0, sdfUv - t1).g;
        float st3 = auxTex.Sample(s0, sdfUv + t2).g;
        float st4 = auxTex.Sample(s0, sdfUv - t2).g;
        float st5 = auxTex.Sample(s0, sdfUv + t3).g;
        float st6 = auxTex.Sample(s0, sdfUv - t3).g;
        // Weights: center 3.0, ±1.25 each 1.0, ±2.5 each 1.0, ±3.5 each 0.5
        sdf = (sdf * 3.0 + st5 + st6 + st3 + st4 + (st1 + st2) * 0.5) / 8.0;
    }

    // Small inward bias to avoid a thin bright fringe on the cave side.
    if (!sharpMode && nativeCurvingEnabled)
        sdf -= 0.012;

    // ---- Cave / solid alpha -----------------------------------------------
    // fwidth-adaptive softness: wider transitions where the SDF changes
    // rapidly in screen space (shallow-angle boundaries).
    float alpha;
    if (sharpMode || !nativeCurvingEnabled)
    {
        // Hard cave/solid cut with no soft transition when smoothing/curving
        // is disabled so visuals match collision-occupied cells.
        alpha = step(0.5, sdf);
    }
    else
    {
        float fw = fwidth(sdf);
        float baseSoftness = max(0.10, NativeContinuousParams.x);
        float edgeSoftness = baseSoftness + fw * 6.0;
        alpha = smoothstep(0.5 - edgeSoftness, 0.5 + edgeSoftness, sdf);
    }

    // ---- Bilinear / nearest blending weight -------------------------------
    // Near the SDF boundary: prefer bilinear (smooth color transitions).
    // Deep inside solid terrain: prefer nearest (sharp cell textures).
    float bilinearWeight = 0.0;
    if (!sharpMode && nativeCurvingEnabled)
    {
        float boundaryProximity = 1.0 - abs(sdf * 2.0 - 1.0);
        bilinearWeight = smoothstep(0.0, 0.6, boundaryProximity) * NativeContinuousParams.y;
    }

    // ---- Material-boundary widening ---------------------------------------
    // At stone-dirt / dirt-energy boundaries (both solid in the SDF), the
    // color contrast between nearest and bilinear reveals a cell edge.
    // Widen the gradient with a 9-tap cross (±1.5 and ±2.5 cells) so the
    // transition spans ~5 cells instead of ~1, reducing the staircase.
    float materialEdgeFactor = 0.0;
    if (!sharpMode)
    {
        float cellEdge = length(nearestColor - bilinearColor);
        materialEdgeFactor = smoothstep(0.04, 0.15, cellEdge);
        if (materialEdgeFactor > 0.01)
        {
            float3 wider = (
                bilinearColor * 2.0 +
                sourceTex.Sample(s0, texUv + float2(cellPx.x * 1.5, 0)).rgb +
                sourceTex.Sample(s0, texUv - float2(cellPx.x * 1.5, 0)).rgb +
                sourceTex.Sample(s0, texUv + float2(0, cellPx.y * 1.5)).rgb +
                sourceTex.Sample(s0, texUv - float2(0, cellPx.y * 1.5)).rgb +
                sourceTex.Sample(s0, texUv + float2(cellPx.x * 2.5, 0)).rgb * 0.5 +
                sourceTex.Sample(s0, texUv - float2(cellPx.x * 2.5, 0)).rgb * 0.5 +
                sourceTex.Sample(s0, texUv + float2(0, cellPx.y * 2.5)).rgb * 0.5 +
                sourceTex.Sample(s0, texUv - float2(0, cellPx.y * 2.5)).rgb * 0.5
            ) / 8.0;
            bilinearColor = lerp(bilinearColor, wider, materialEdgeFactor);
        }
    }

    // Force high bilinear weight at detected material edges.
    float materialEdgeBilinear = materialEdgeFactor * 0.95;
    bilinearWeight = max(bilinearWeight, materialEdgeBilinear);

    float3 solidColor = lerp(nearestColor, bilinearColor, saturate(bilinearWeight));

    // ---- Final compositing ------------------------------------------------
    float3 caveColor = float3(0.055, 0.055, 0.063);
    float3 color = lerp(caveColor, solidColor, alpha);
    return float4(color, 1.0);
}
