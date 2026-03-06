float SampleLandformHeight(float2 worldCell)
{
    float broad = fbmNoise(worldCell * 0.0075 + float2(71.0, 911.0), 4);
    float folded = fbmNoise(float2(
        worldCell.x * 0.006 + worldCell.y * 0.002,
        -worldCell.x * 0.002 + worldCell.y * 0.006) + float2(313.0, 509.0), 3);
    return saturate(broad * 0.62 + folded * 0.58);
}

void ApplyStoneMaterial(inout float3 color, float2 worldCell, float materialMask, float3 lightDir)
{
    if (materialMask <= 0.0) return;

    // Large-scale structure (hills, ridges, plateaus, cliffs).
    float land = SampleLandformHeight(worldCell);
    float landX = SampleLandformHeight(worldCell + float2(4.0, 0.0));
    float landY = SampleLandformHeight(worldCell + float2(0.0, 4.0));
    float2 landGrad = float2(landX - land, landY - land);
    float slope = length(landGrad);
    float cliff = smoothstep(0.018, 0.052, slope);
    float ridgeBand = 1.0 - abs(land * 2.0 - 1.0);
    float ridges = pow(saturate(ridgeBand), 3.2);
    float plateau = smoothstep(0.66, 0.90, land);
    float hill = smoothstep(0.28, 0.68, land) * (1.0 - plateau);
    float terrace = smoothstep(0.72, 0.98, 1.0 - abs(frac(land * 7.0) * 2.0 - 1.0));
    color *= 1.0 - materialMask * (0.020 + 0.070 * cliff + 0.016 * (1.0 - ridges) + 0.018 * terrace);
    color = lerp(color, color * (0.88 + 0.28 * hill + 0.14 * plateau), materialMask * 0.78);

    // Base relief with ridged breakup.
    float2 baseP = worldCell * 0.026;
    float b0 = fbmNoise(baseP + float2(17.0, 3.0), 5);
    float bnx = fbmNoise(baseP + float2(19.0, 3.0), 5);
    float bny = fbmNoise(baseP + float2(17.0, 5.0), 5);
    float2 bGrad = float2(bnx - b0, bny - b0);
    float ridgedBase = 1.0 - abs(b0 * 2.0 - 1.0);
    float3 baseNormal = normalize(float3(-bGrad * 11.0, 1.0));
    float baseDiffuse = dot(baseNormal, lightDir) * 0.5 + 0.5;
    float baseCavity = smoothstep(0.55, 0.10, b0);
    float baseDark = (0.04 + 0.12 * baseCavity + 0.05 * ridgedBase) * materialMask;
    color *= 1.0 - baseDark;
    color = lerp(color, color * (0.76 + 0.42 * baseDiffuse), materialMask * 0.88);

    // High-frequency chipped grain.
    float2 grainP = worldCell * 0.145;
    float g0 = fbmNoise(grainP + float2(121.0, 33.0), 3);
    float gnx = fbmNoise(grainP + float2(123.0, 33.0), 3);
    float gny = fbmNoise(grainP + float2(121.0, 35.0), 3);
    float2 gGrad = float2(gnx - g0, gny - g0);
    float3 grainNormal = normalize(float3(-gGrad * 5.3, 1.0));
    float grainDiffuse = dot(grainNormal, lightDir) * 0.5 + 0.5;
    float grainMask = materialMask * 0.75;
    float grainCavity = smoothstep(0.60, 0.20, g0);
    color *= 1.0 - grainMask * (0.015 + 0.045 * grainCavity);
    color = lerp(color, color * (0.88 + 0.22 * grainDiffuse), grainMask);

    // Angular strata and fracture lines for wall-like structure.
    float2 strataP = float2(
        worldCell.x * 0.060 + worldCell.y * 0.022,
        -worldCell.x * 0.022 + worldCell.y * 0.060);
    float strataNoise = fbmNoise(strataP + float2(211.0, 87.0), 4);
    float ridge = 1.0 - abs(strataNoise * 2.0 - 1.0);
    float strata = pow(saturate(ridge), 3.2);
    float crack = smoothstep(0.80, 0.985, strata);
    color *= 1.0 - materialMask * (0.055 * strata + 0.090 * crack);

    // Block/chisel breakup from coarse cell noise.
    float2 blockP = worldCell * 0.095;
    float2 blockCell = floor(blockP);
    float2 blockLocal = frac(blockP) - 0.5;
    float blockRnd = hash21(blockCell + float2(401.0, 59.0));
    float ang = blockRnd * 6.2831853;
    float2 blockDir = normalize(float2(cos(ang), sin(ang)) + float2(1e-4, -1e-4));
    float chisel = 0.5 + dot(blockLocal, blockDir) * 1.9;
    float blockEdge = smoothstep(0.32, 0.50, max(abs(blockLocal.x), abs(blockLocal.y)));
    color *= 1.0 - materialMask * (0.020 * saturate(chisel) + 0.040 * blockEdge);

    // Macro breakup prevents broad uniform patches.
    float macro = fbmNoise(worldCell * 0.011 + float2(301.0, 133.0), 3);
    float macroShade = lerp(0.86, 1.12, macro);
    color = lerp(color, color * macroShade, materialMask * 0.45);

    // Extra stone detail: mineral veins + pitted weathering.
    float2 veinP = float2(
        worldCell.x * 0.082 + worldCell.y * 0.031,
        -worldCell.x * 0.031 + worldCell.y * 0.082);
    float veinA = 1.0 - abs(fbmNoise(veinP + float2(719.0, 41.0), 3) * 2.0 - 1.0);
    float veinB = 1.0 - abs(fbmNoise(veinP * 1.35 + float2(271.0, 503.0), 3) * 2.0 - 1.0);
    float veinMask = max(smoothstep(0.86, 0.985, veinA), smoothstep(0.90, 0.992, veinB));
    veinMask *= materialMask * smoothstep(0.30, 0.80, fbmNoise(worldCell * 0.090 + float2(149.0, 911.0), 2));
    color *= 1.0 - veinMask * 0.060;
    color = lerp(color, color * float3(1.06, 1.02, 0.95), veinMask * 0.16);

    float pit = smoothstep(0.72, 0.16, fbmNoise(worldCell * 0.235 + float2(607.0, 263.0), 2));
    float chipDust = fbmNoise(worldCell * 0.520 + float2(181.0, 73.0), 2);
    color *= 1.0 - materialMask * (0.016 * pit);
    color *= lerp(0.96, 1.06, chipDust * materialMask);
}

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
