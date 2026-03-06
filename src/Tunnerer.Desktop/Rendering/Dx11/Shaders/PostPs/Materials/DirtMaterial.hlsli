void ApplyDirtMaterial(inout float3 color, float2 worldCell, float dirtMask, float3 lightDir)
{
    if (dirtMask <= 0.0) return;

    float2 warp0 = float2(
        fbmNoise(worldCell * 0.020 + float2(43.0, 197.0), 3),
        fbmNoise(worldCell * 0.020 + float2(181.0, 67.0), 3));
    float2 warp1 = float2(
        fbmNoise(worldCell * 0.045 + float2(313.0, 11.0), 2),
        fbmNoise(worldCell * 0.045 + float2(97.0, 419.0), 2));
    float2 dirtP = worldCell * 0.070 + (warp0 - 0.5) * 0.35 + (warp1 - 0.5) * 0.15;

    float d0 = fbmNoise(dirtP + float2(19.0, 201.0), 4);
    float dnx = fbmNoise(dirtP + float2(21.0, 201.0), 4);
    float dny = fbmNoise(dirtP + float2(19.0, 203.0), 4);
    float2 dGrad = float2(dnx - d0, dny - d0);

    float dg0 = fbmNoise(dirtP * 2.6 + float2(503.0, 71.0), 3);
    float dgnx = fbmNoise(dirtP * 2.6 + float2(505.0, 71.0), 3);
    float dgny = fbmNoise(dirtP * 2.6 + float2(503.0, 73.0), 3);
    float2 dgGrad = float2(dgnx - dg0, dgny - dg0);

    float3 dirtNormal = normalize(float3(-(dGrad * 6.0 + dgGrad * 2.2), 1.0));
    float dirtDiffuse = dot(dirtNormal, lightDir) * 0.5 + 0.5;

    // Macro landform structure for dirt: hills, plateaus, and eroded cliff bands.
    float land = SampleLandformHeight(worldCell + float2(91.0, 47.0));
    float landX = SampleLandformHeight(worldCell + float2(95.0, 47.0));
    float landY = SampleLandformHeight(worldCell + float2(91.0, 51.0));
    float2 landGrad = float2(landX - land, landY - land);
    float slope = length(landGrad);
    float dirtCliff = smoothstep(0.016, 0.046, slope);
    float dirtPlateau = smoothstep(0.63, 0.88, land);
    float dirtHill = smoothstep(0.30, 0.66, land) * (1.0 - dirtPlateau);
    float dirtTerrace = smoothstep(0.76, 0.98, 1.0 - abs(frac(land * 6.0) * 2.0 - 1.0));

    // Multi-scale dirt structure with non-periodic, high-frequency detail.
    float clump = smoothstep(0.18, 0.86, d0);
    float moisture = fbmNoise(worldCell * 0.016 + float2(311.0, 97.0), 4);
    float pore = smoothstep(0.64, 0.14, fbmNoise(worldCell * 0.155 + float2(71.0, 13.0), 3));
    float looseDust = fbmNoise(worldCell * 0.105 + float2(151.0, 39.0), 3);
    float grain = fbmNoise(worldCell * 0.290 + float2(433.0, 29.0), 2);
    float grit = fbmNoise(worldCell * 0.470 + float2(263.0, 149.0), 2);

    // Irregular fissures from warped directional ridges (no cell/rectangular outlines).
    float2 cP = worldCell * 0.19 + (warp0 - 0.5) * 0.2;
    float c1 = 1.0 - abs(fbmNoise(float2(cP.x * 1.75 + cP.y * 0.33, -cP.x * 0.33 + cP.y * 1.75) + float2(701.0, 19.0), 3) * 2.0 - 1.0);
    float c2 = 1.0 - abs(fbmNoise(float2(cP.x * 1.31 - cP.y * 0.58, cP.x * 0.58 + cP.y * 1.31) + float2(211.0, 907.0), 3) * 2.0 - 1.0);
    float c3 = 1.0 - abs(fbmNoise(cP * 2.6 + float2(47.0, 613.0), 2) * 2.0 - 1.0);
    float crackField = max(max(c1, c2), c3);
    float crackMask = smoothstep(0.95, 0.997, crackField);
    float crackBreak = smoothstep(0.24, 0.80, fbmNoise(cP * 0.9 + float2(611.0, 43.0), 3));
    crackMask *= crackBreak;

    // Broad sparse fissures for macro soil breakup.
    float2 macroP = worldCell * 0.060 + (warp0 - 0.5) * 0.25;
    float m1 = 1.0 - abs(fbmNoise(float2(macroP.x * 1.08 + macroP.y * 0.20, -macroP.x * 0.20 + macroP.y * 1.08) + float2(131.0, 761.0), 3) * 2.0 - 1.0);
    float m2 = 1.0 - abs(fbmNoise(float2(macroP.x * 0.88 - macroP.y * 0.43, macroP.x * 0.43 + macroP.y * 0.88) + float2(521.0, 227.0), 3) * 2.0 - 1.0);
    float macroRidge = max(m1, m2);
    float macroFissure = smoothstep(0.965, 0.998, macroRidge);
    macroFissure *= smoothstep(0.32, 0.84, fbmNoise(macroP * 0.65 + float2(83.0, 947.0), 3));

    // Sparse pebbly minerals without grid repetition.
    float pebbleSeed = fbmNoise(worldCell * 0.39 + float2(91.0, 17.0), 2);
    float pebbleMask = smoothstep(0.87, 0.975, pebbleSeed) * smoothstep(0.35, 0.84, fbmNoise(worldCell * 0.22 + float2(239.0, 311.0), 2));

    float dirtDark = 0.020 + 0.054 * pore + 0.030 * (1.0 - clump) + 0.024 * (1.0 - moisture);
    float3 drySoil = float3(0.91, 0.63, 0.33);
    float3 wetSoil = float3(0.46, 0.28, 0.15);
    float3 rustySoil = float3(0.66, 0.34, 0.17);
    float warmMineral = fbmNoise(dirtP * 0.85 + float2(801.0, 53.0), 3);
    float3 dirtTint = lerp(drySoil, wetSoil, saturate(1.0 - moisture * 0.9));
    dirtTint = lerp(dirtTint, rustySoil, smoothstep(0.58, 0.90, warmMineral) * 0.35);

    float3 dirtColor = dirtTint * (0.82 + 0.26 * dirtDiffuse);
    dirtColor *= 1.0 - dirtDark;
    dirtColor *= lerp(0.86, 1.14, looseDust);
    dirtColor *= lerp(0.88, 1.14, grain);
    dirtColor *= lerp(0.93, 1.10, grit);
    float dapple = fbmNoise(worldCell * 0.22 + float2(149.0, 701.0), 2);
    dirtColor *= lerp(0.94, 1.06, dapple);
    float microSpark = fbmNoise(worldCell * 0.95 + float2(81.0, 403.0), 2);
    dirtColor *= lerp(0.91, 1.10, microSpark);
    float microDustA = fbmNoise(worldCell * 1.35 + float2(607.0, 281.0), 2);
    float microDustB = fbmNoise(float2(
        worldCell.x * 1.05 + worldCell.y * 0.37,
        -worldCell.x * 0.37 + worldCell.y * 1.05) + float2(211.0, 1181.0), 2);
    dirtColor *= lerp(0.90, 1.12, microDustA);
    dirtColor *= lerp(0.93, 1.09, microDustB);
    float tinyRidge = 1.0 - abs(fbmNoise(float2(
        worldCell.x * 0.72 + worldCell.y * 0.19,
        -worldCell.x * 0.19 + worldCell.y * 0.72) + float2(907.0, 151.0), 2) * 2.0 - 1.0);
    float tinyCrack = smoothstep(0.93, 0.995, tinyRidge) * (0.7 + 0.3 * (1.0 - moisture));
    dirtColor *= 1.0 - tinyCrack * 0.032;
    dirtColor *= 1.0 - crackMask * (0.045 + 0.055 * (1.0 - moisture));
    dirtColor *= 1.0 - macroFissure * (0.060 + 0.050 * (1.0 - moisture));
    dirtColor *= 1.0 - dirtCliff * 0.070 - dirtTerrace * 0.030;
    dirtColor *= 1.0 + dirtHill * 0.070 + dirtPlateau * 0.040;
    // Additional dirt richness: clod rims and tiny mineral sparkle.
    float clodRim = smoothstep(0.56, 0.94, clump) * (1.0 - smoothstep(0.78, 0.98, clump));
    float microSpec = smoothstep(0.90, 0.995, fbmNoise(worldCell * 1.25 + float2(937.0, 127.0), 2));
    dirtColor *= 1.0 + clodRim * 0.040;
    dirtColor = lerp(dirtColor, dirtColor * float3(1.04, 1.02, 0.98), microSpec * 0.10 * (1.0 - moisture));
    dirtColor = lerp(dirtColor, dirtColor * float3(0.77, 0.72, 0.68), pebbleMask * 0.20);

    // Dirt visuals are fully procedural and not dependent on base terrain dirt pixels.
    color = lerp(color, dirtColor, dirtMask);
}
