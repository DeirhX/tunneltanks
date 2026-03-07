// ============================================================================
// Passes/DistortionPass.hlsli — Heat-haze UV distortion
// ============================================================================
//
// Computes per-pixel UV offsets that simulate heat shimmer around hot tanks.
// Two layers of distortion are combined:
//
//   1. Tank-centered haze: radial falloff from each tank glow source, with
//      swirl-direction shimmer that intensifies on the tank's edge band.
//      Also includes dedicated overheat edge wobble for tanks above 50% heat.
//
//   2. Outline-based shimmer: detects geometric silhouettes (luminance +
//      alpha edges) and adds tangential wobble so hot outlines visually waver.
//
// Both effects are animated with multi-frequency sine waves for organic motion.
//
// Outputs:
//   hazeOffset       — accumulated UV displacement to apply to sceneTex fetch
//   outlineHeatMask  — [0..1] mask of overheat proximity for outline shimmer
//   distortionEnabled — 0 or 1, driven by TankHeatGlowColor.a
// ============================================================================

void ComputeTankHeatHaze(float2 uv, out float2 hazeOffset, out float outlineHeatMask, out float distortionEnabled)
{
    hazeOffset = float2(0.0, 0.0);
    outlineHeatMask = 0.0;
    distortionEnabled = step(0.5, TankHeatGlowColor.a);

    [loop]
    for (int i = 0; i < 8; i++)
    {
        if (distortionEnabled <= 0.0) break;
        if (i >= (int)TankGlowCount) break;
        float4 g = TankGlow[i];
        float heatT = saturate(g.w);
        float2 d = uv - g.xy;
        float2 dPx = d * ViewSize;
        float dist2 = dot(dPx, dPx);
        float radiusPx = max(kRadiusEpsilonPx, g.z * max(ViewSize.x, ViewSize.y));
        float radius2 = radiusPx * radiusPx;
        float falloff = saturate(1.0 - dist2 / radius2);
        if (falloff <= 0.0) continue;

        // ---- Swirl haze around the tank -----------------------------------
        float distNorm = sqrt(saturate(1.0 - falloff));
        float tankEdgeBand = smoothstep(0.34, 0.16, distNorm) * (1.0 - smoothstep(0.16, 0.03, distNorm));
        float2 swirlDirPx = normalize(float2(-dPx.y, dPx.x) + kSignedEpsilon2);
        float shimmer =
            sin(Time * 14.0 + (uv.x * 210.0 + uv.y * 170.0) + i * 1.7) * 0.65 +
            sin(Time * 22.0 + (uv.x * 120.0 - uv.y * 140.0) + i * 2.3) * 0.35;
        float heatGate = smoothstep(0.40, 0.75, heatT);
        float hazeStrengthPx = heatGate * (0.8 + 4.0 * heatT) * (0.35 + 0.65 * tankEdgeBand) * falloff;
        hazeOffset += swirlDirPx * shimmer * hazeStrengthPx * TexelSize;

        // ---- Overheat (50%+) edge wobble ----------------------------------
        float over50 = smoothstep(0.78, 0.95, heatT);
        outlineHeatMask += over50 * falloff;

        float2 tankEdgeDirPx = normalize(float2(dPx.y, -dPx.x) + kSignedEpsilon2);
        float tankWave =
            sin(Time * 31.0 + distNorm * 96.0 + i * 2.4) * 0.65 +
            sin(Time * 19.0 - distNorm * 71.0 + i * 1.3) * 0.35;
        float edgeWobblePx = over50 * tankEdgeBand * (0.40 + 2.20 * heatT);
        hazeOffset += tankEdgeDirPx * tankWave * edgeWobblePx * TexelSize;
    }

    outlineHeatMask = saturate(outlineHeatMask);
}

// ============================================================================
// Outline-based heat shimmer
// ============================================================================
// Adds extra UV wobble on geometric silhouettes (terrain edges, tank outlines)
// when nearby tanks are overheated. Uses luminance + alpha edge detection to
// find silhouettes, then applies tangent-direction sine-wave displacement.
// ============================================================================

void ApplyOutlineHeatDistortion(float2 uv, float distortionEnabled, float outlineHeatMask, inout float2 hazeOffset)
{
    if (distortionEnabled <= 0.0 || outlineHeatMask <= 0.0)
        return;

    float2 tx = TexelOffsetX();
    float2 ty = TexelOffsetY();

    // ---- Edge detection (luminance + alpha) -------------------------------
    float3 cL = sceneTex.Sample(s0, uv - tx).rgb;
    float3 cR = sceneTex.Sample(s0, uv + tx).rgb;
    float3 cU = sceneTex.Sample(s0, uv - ty).rgb;
    float3 cD = sceneTex.Sample(s0, uv + ty).rgb;
    float aL = sceneTex.Sample(s0, uv - tx).a;
    float aR = sceneTex.Sample(s0, uv + tx).a;
    float aU = sceneTex.Sample(s0, uv - ty).a;
    float aD = sceneTex.Sample(s0, uv + ty).a;

    float lL = dot(cL, kLumaWeights);
    float lR = dot(cR, kLumaWeights);
    float lU = dot(cU, kLumaWeights);
    float lD = dot(cD, kLumaWeights);
    float2 edgeGrad = float2(lR - lL, lD - lU);

    float edgeLum = saturate(length(edgeGrad) * 3.5);
    float edgeAlpha = saturate((abs(aR - aL) + abs(aD - aU)) * 2.8);
    float outlineEdge = max(edgeLum, edgeAlpha);

    // ---- Tangential wobble on detected edges ------------------------------
    float2 edgeDir = normalize(float2(edgeGrad.y, -edgeGrad.x) + kSignedEpsilon2);
    float outlineWave =
        sin(Time * 28.0 + uv.x * 260.0 - uv.y * 220.0) * 0.60 +
        sin(Time * 17.0 + uv.x * 120.0 + uv.y * 145.0) * 0.40;
    hazeOffset += edgeDir * outlineWave * (outlineHeatMask * outlineEdge * 0.012);
}
