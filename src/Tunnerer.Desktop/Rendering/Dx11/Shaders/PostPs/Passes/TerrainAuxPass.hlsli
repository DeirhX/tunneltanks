void ApplyTerrainHeatAndScorch(inout float3 color, float2 worldCell, float2 auxUv, float2 mTexel, float terrainFactor, float4 a0, float4 ax1, float4 ax2, float4 ay1, float4 ay2, float4 ad1, float4 ad2, float4 ad3, float4 ad4)
{
    // Local + wider blur so boosted low-temperature glow does not form hard contours.
    float heatLocal = a0.r * 0.30
        + (ax1.r + ax2.r + ay1.r + ay2.r) * 0.10
        + (ad1.r + ad2.r + ad3.r + ad4.r) * 0.05;
    float2 mTexel2 = mTexel * 2.0;
    float heatWide =
        auxTex.Sample(s0, auxUv + float2(mTexel2.x, 0.0)).r * 0.25 +
        auxTex.Sample(s0, auxUv - float2(mTexel2.x, 0.0)).r * 0.25 +
        auxTex.Sample(s0, auxUv + float2(0.0, mTexel2.y)).r * 0.25 +
        auxTex.Sample(s0, auxUv - float2(0.0, mTexel2.y)).r * 0.25;
    // Detect steep thermal transitions so we can soften hard contour rings.
    float heatGrad = abs(ax1.r - ax2.r) + abs(ay1.r - ay2.r);
    float heatEdge = saturate(heatGrad * 1.80);
    float heatBlend = lerp(0.35, 0.75, heatEdge);
    float heat = lerp(heatLocal, heatWide, heatBlend);

    // Anti-aliased fade-in: widen transition where heat gradient is steep.
    float edgeSoft = max(0.008, fwidth(heat) * (4.0 + 4.0 * heatEdge));
    float fadeIn = smoothstep(0.002 - edgeSoft, 0.09 + edgeSoft, heat);
    float heatNorm = heat * fadeIn;

    // Keep strong saturation/intensity while smoothly masking low-end onset.
    float t = pow(saturate(heatNorm), lerp(0.62, 0.86, heatEdge));
    float3 glow = HeatRamp(t);
    float heatGain = lerp(2.00, 2.35, t) * lerp(1.0, 0.92, heatEdge);
    float visible = smoothstep(0.0, lerp(0.040, 0.085, heatEdge), heatNorm);
    color += glow * heatGain * visible;

    float phase = frac(sin(dot(floor(worldCell), float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
    float pulse = MaterialEmissivePulse.y + MaterialEmissivePulse.z * (0.5 + 0.5 * sin(Time * MaterialEmissivePulse.x + phase));
    float scorch = a0.a * 0.50 + (ax1.a + ax2.a + ay1.a + ay2.a) * 0.10 + (ad1.a + ad2.a + ad3.a + ad4.a) * 0.025;
    color += MaterialEmissiveScorched.rgb * (scorch * MaterialEmissiveScorched.a * pulse * terrainFactor);
}

void ApplyTerrainAuxPass(float2 uv, float terrainFactor, inout float3 color)
{
    if (UseTerrainAux <= 0.5 || PixelScale <= 0.0)
        return;

    float2 screenPx = uv * ViewSize;
    float2 worldCell = (CameraPixels + screenPx) / PixelScale;
    float2 auxUv = worldCell / WorldSize;
    float2 mTexel = float2(1.0 / WorldSize.x, 1.0 / WorldSize.y);
    float4 a0 = auxTex.Sample(s0, auxUv);
    float4 ax1 = auxTex.Sample(s0, auxUv + float2(mTexel.x, 0.0));
    float4 ax2 = auxTex.Sample(s0, auxUv - float2(mTexel.x, 0.0));
    float4 ay1 = auxTex.Sample(s0, auxUv + float2(0.0, mTexel.y));
    float4 ay2 = auxTex.Sample(s0, auxUv - float2(0.0, mTexel.y));
    float4 ad1 = auxTex.Sample(s0, auxUv + float2(mTexel.x, mTexel.y));
    float4 ad2 = auxTex.Sample(s0, auxUv + float2(mTexel.x, -mTexel.y));
    float4 ad3 = auxTex.Sample(s0, auxUv + float2(-mTexel.x, mTexel.y));
    float4 ad4 = auxTex.Sample(s0, auxUv + float2(-mTexel.x, -mTexel.y));

    float m0 = a0.g;
    float mx1 = ax1.g;
    float mx2 = ax2.g;
    float my1 = ay1.g;
    float my2 = ay2.g;

    // Sobel gradient of the smooth SDF — used for edge detection and normals
    float gx = (ad2.g + 2.0 * mx1 + ad1.g) - (ad4.g + 2.0 * mx2 + ad3.g);
    float gy = (ad3.g + 2.0 * my1 + ad1.g) - (ad4.g + 2.0 * my2 + ad2.g);
    float edge = length(float2(gx, gy)) * 0.25;
    float edgeAmt = min(1.0, edge * TerrainMaskEdgeStrength);

    // SDF is already smooth — light averaging to reduce any quantization artifacts
    float m = lerp(m0, (mx1 + mx2 + my1 + my2) * 0.25, 0.15);
    float boundary = 1.0 - abs(m * 2.0 - 1.0);
    float outline = min(1.0, boundary * TerrainMaskBoundaryScale);
    float maskWidth = max(fwidth(m) * 1.4, 0.02);
    float maskSoft = smoothstep(0.5 - maskWidth, 0.5 + maskWidth, m);
    float edgeProfile = edgeAmt * smoothstep(0.05, 0.8, boundary);
    // Remove edge darkening; keep curvature/lift shaping only.
    color += maskSoft * edgeProfile * TerrainMaskSolidLift * terrainFactor;
    color += maskSoft * edgeProfile * outline * TerrainMaskRimLift * terrainFactor;

    // Material classes come from aux.b (0=None, 85=Dirt, 170=Stone, 212=Energy, 255=Base).
    int2 auxMax = int2(max(1.0, WorldSize.x), max(1.0, WorldSize.y)) - int2(1, 1);
    int2 auxCell = clamp(int2(floor(worldCell)), int2(0, 0), auxMax);
    float4 aNearest = auxTex.Load(int3(auxCell, 0));
    float materialCode = aNearest.b * kMaterialCodeScale;
    float dirtMask = (1.0 - smoothstep(0.0, kMaterialMaskWidthWide, abs(materialCode - kMaterialCodeDirt))) * terrainFactor;
    float stoneMask = (1.0 - smoothstep(0.0, kMaterialMaskWidthWide, abs(materialCode - kMaterialCodeStone))) * terrainFactor;
    float energyMask = (1.0 - smoothstep(0.0, kMaterialMaskWidthNarrow, abs(materialCode - kMaterialCodeEnergy))) * terrainFactor;
    float baseMask = (1.0 - smoothstep(0.0, kMaterialMaskWidthNarrow, abs(materialCode - kMaterialCodeBase))) * terrainFactor;
    stoneMask = saturate(stoneMask + baseMask * 0.75);
    dirtMask = saturate(dirtMask * (1.0 - stoneMask * 0.6) * (1.0 - energyMask));

    float3 lightDir = normalize(float3(LightDir.xy, max(0.35, LightDir.z)));
    ApplyStoneMaterial(color, worldCell, stoneMask, lightDir);
    ApplyDirtMaterial(color, worldCell, dirtMask, lightDir);
    ApplyEnergyMaterial(color, worldCell, energyMask, lightDir);

    float3 aaNeighborhood =
        sceneTex.Sample(s0, uv + float2(mTexel.x, 0.0)).rgb +
        sceneTex.Sample(s0, uv - float2(mTexel.x, 0.0)).rgb +
        sceneTex.Sample(s0, uv + float2(0.0, mTexel.y)).rgb +
        sceneTex.Sample(s0, uv - float2(0.0, mTexel.y)).rgb +
        sceneTex.Sample(s0, uv + float2(mTexel.x, mTexel.y)).rgb +
        sceneTex.Sample(s0, uv + float2(mTexel.x, -mTexel.y)).rgb +
        sceneTex.Sample(s0, uv + float2(-mTexel.x, mTexel.y)).rgb +
        sceneTex.Sample(s0, uv + float2(-mTexel.x, -mTexel.y)).rgb;
    aaNeighborhood *= (1.0 / 8.0);
    float aaMix = saturate(edgeProfile * 0.30) * terrainFactor;
    color = lerp(color, aaNeighborhood, aaMix);

    ApplyTerrainHeatAndScorch(color, worldCell, auxUv, mTexel, terrainFactor, a0, ax1, ax2, ay1, ay2, ad1, ad2, ad3, ad4);

    // Heat debug overlay: half-transparent temperature map (blue->yellow->red).
    if (HeatDebugOverlay > 0.5)
    {
        float temperature = a0.r * kHeatDebugTemperatureScale;
        float heat01 = saturate(temperature / 100.0);
        color = lerp(color, HeatDebugRamp(heat01), 0.5);
    }
}
