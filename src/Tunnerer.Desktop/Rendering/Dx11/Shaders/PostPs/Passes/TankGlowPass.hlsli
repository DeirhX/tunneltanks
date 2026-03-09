// ============================================================================
// Passes/TankGlowPass.hlsli — Per-tank heat glow and hot-metal shading
// ============================================================================
//
// Iterates over up to 8 tank glow sources (TankGlow[]) and applies:
//
//   1. Hot-metal tint — entity pixels (alpha < 1) inside the glow radius
//      are tinted from cold steel blue toward orange/red as heat rises.
//      A white-hot specular core brightens the very center.
//
//   2. Self-emission — tank body pixels additively glow with the heat color,
//      scaling quadratically with temperature for a visible ramp-up.
//
//   3. Animated shimmer — multi-frequency sine waves modulate body glow and
//      rim brightness so hot tanks appear to pulse and ripple.
//
// The pass only affects entity pixels; terrain is excluded by the entity mask.
// ============================================================================

void ApplyTankHeatGlowPass(float2 uv, inout float3 color)
{
    // Keep only heat haze distortion from DistortionPass. Tank color/glow
    // overshoots the palette for now, so this pass is intentionally disabled.
    return;

    // Entity mask: 1 for entity pixels, 0 for terrain.
    float entityMaskBase = 1.0 - step(kTerrainAlphaThreshold, sceneTex.Sample(s0, uv).a);

    [loop]
    for (int j = 0; j < 8; j++)
    {
        if (j >= (int)TankGlowCount) break;
        float4 g = TankGlow[j];
        float heatT = saturate(g.w);
        float2 d = uv - g.xy;
        float2 dPx = d * ViewSize;
        float radiusPx = max(kRadiusEpsilonPx, g.z * max(ViewSize.x, ViewSize.y));
        float falloff = 1.0 - dot(dPx, dPx) / (radiusPx * radiusPx);
        if (falloff <= 0.0)
            continue;

        // ---- Radial falloff shapes ----------------------------------------
        float f2 = falloff * falloff;
        float halo = saturate(pow(saturate(falloff), 0.55) - f2 * 0.35);
        float core = pow(saturate(falloff), 0.30);

        // ---- Hot-metal tint -----------------------------------------------
        // Transition from cold steel to glowing orange/red based on heat.
        float orangeT = smoothstep(0.86, 1.0, heatT);
        float3 tankHeatColor = lerp(float3(0.95, 0.08, 0.02), float3(1.00, 0.46, 0.06), orangeT);
        float metalHot = smoothstep(0.28, 1.0, heatT);
        float metalMask = entityMaskBase * core * (0.20 + 0.80 * metalHot);
        float baseLum = dot(color, kLumaWeights);
        float3 steelBase = lerp(float3(0.18, 0.22, 0.28), float3(0.34, 0.38, 0.44), saturate(baseLum * 1.5));
        float3 hotMetal = lerp(steelBase, tankHeatColor, metalHot);
        hotMetal += float3(1.00, 0.92, 0.70) * pow(core, 4.0) * (0.08 + 0.34 * heatT);
        color = lerp(color, color * 0.30 + hotMetal * 1.18, saturate(metalMask));

        // ---- Self-emission ------------------------------------------------
        float coreBoost = heatT * (0.45 + 1.15 * heatT);
        float bodyHeat = coreBoost * core * entityMaskBase;
        color += tankHeatColor * bodyHeat;

        // ---- Animated body shimmer ----------------------------------------
        float bodyShimmer = 0.5 + 0.5 * sin(Time * 16.0 + (uv.x * 190.0 + uv.y * 160.0) + j * 1.9);
        float bodyShimmerAmp = lerp(0.10, 0.35, heatT);
        float rimShimmer = heatT * halo * (0.02 + 0.12 * bodyShimmer);
        color += tankHeatColor * (bodyHeat * (0.06 + bodyShimmerAmp * bodyShimmer) + rimShimmer);
    }
}
