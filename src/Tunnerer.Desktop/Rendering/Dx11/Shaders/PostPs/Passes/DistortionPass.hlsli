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
        float radiusPx = max(1e-3, g.z * max(ViewSize.x, ViewSize.y));
        float radius2 = radiusPx * radiusPx;
        float falloff = saturate(1.0 - dist2 / radius2);
        if (falloff <= 0.0) continue;

        float distNorm = sqrt(saturate(1.0 - falloff));
        float tankEdgeBand = smoothstep(0.34, 0.16, distNorm) * (1.0 - smoothstep(0.16, 0.03, distNorm));
        float2 swirlDirPx = normalize(float2(-dPx.y, dPx.x) + float2(1e-4, -1e-4));
        float shimmer =
            sin(Time * 14.0 + (uv.x * 210.0 + uv.y * 170.0) + i * 1.7) * 0.65 +
            sin(Time * 22.0 + (uv.x * 120.0 - uv.y * 140.0) + i * 2.3) * 0.35;
        float heatGate = smoothstep(0.40, 0.75, heatT);
        float hazeStrengthPx = heatGate * (0.8 + 4.0 * heatT) * (0.35 + 0.65 * tankEdgeBand) * falloff;
        hazeOffset += swirlDirPx * shimmer * hazeStrengthPx * TexelSize;

        // Overheat(50+) mask derived from glow intensity; used for outline-only shimmer below.
        float over50 = smoothstep(0.78, 0.95, heatT);
        outlineHeatMask += over50 * falloff;

        // Dedicated hot-tank edge wobble so tank outlines visibly shimmer once heat is high.
        float2 tankEdgeDirPx = normalize(float2(dPx.y, -dPx.x) + float2(1e-4, -1e-4));
        float tankWave =
            sin(Time * 31.0 + distNorm * 96.0 + i * 2.4) * 0.65 +
            sin(Time * 19.0 - distNorm * 71.0 + i * 1.3) * 0.35;
        float edgeWobblePx = over50 * tankEdgeBand * (0.40 + 2.20 * heatT);
        hazeOffset += tankEdgeDirPx * tankWave * edgeWobblePx * TexelSize;
    }

    outlineHeatMask = saturate(outlineHeatMask);
}

void ApplyOutlineHeatDistortion(float2 uv, float distortionEnabled, float outlineHeatMask, inout float2 hazeOffset)
{
    // Extra distortion on geometric outlines (terrain+tanks) once tanks are sufficiently hot.
    if (distortionEnabled <= 0.0 || outlineHeatMask <= 0.0)
        return;

    float2 tx = float2(TexelSize.x, 0.0);
    float2 ty = float2(0.0, TexelSize.y);
    float3 cL = sceneTex.Sample(s0, uv - tx).rgb;
    float3 cR = sceneTex.Sample(s0, uv + tx).rgb;
    float3 cU = sceneTex.Sample(s0, uv - ty).rgb;
    float3 cD = sceneTex.Sample(s0, uv + ty).rgb;
    float aL = sceneTex.Sample(s0, uv - tx).a;
    float aR = sceneTex.Sample(s0, uv + tx).a;
    float aU = sceneTex.Sample(s0, uv - ty).a;
    float aD = sceneTex.Sample(s0, uv + ty).a;

    float lL = dot(cL, float3(0.299, 0.587, 0.114));
    float lR = dot(cR, float3(0.299, 0.587, 0.114));
    float lU = dot(cU, float3(0.299, 0.587, 0.114));
    float lD = dot(cD, float3(0.299, 0.587, 0.114));
    float2 edgeGrad = float2(lR - lL, lD - lU);

    float edgeLum = saturate(length(edgeGrad) * 3.5);
    float edgeAlpha = saturate((abs(aR - aL) + abs(aD - aU)) * 2.8);
    float outlineEdge = max(edgeLum, edgeAlpha);

    // Shimmer tangent to the edge gradient so silhouettes appear to wobble.
    float2 edgeDir = normalize(float2(edgeGrad.y, -edgeGrad.x) + float2(1e-4, -1e-4));
    float outlineWave =
        sin(Time * 28.0 + uv.x * 260.0 - uv.y * 220.0) * 0.60 +
        sin(Time * 17.0 + uv.x * 120.0 + uv.y * 145.0) * 0.40;
    hazeOffset += edgeDir * outlineWave * (outlineHeatMask * outlineEdge * 0.012);
}
