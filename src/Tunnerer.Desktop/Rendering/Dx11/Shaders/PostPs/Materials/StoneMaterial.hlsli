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
    float2 blockDir = normalize(float2(cos(ang), sin(ang)) + kSignedEpsilon2);
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
