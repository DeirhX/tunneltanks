// ============================================================================
// Materials/EnergyMaterial.hlsli — Procedural energy crystal overlay
// ============================================================================
//
// Applies a smooth crystalline body tint and pulsing emissive veins to pixels
// classified as energy terrain. Simpler than stone/dirt since the energy
// material is meant to look uniform and glowing.
//
// Layers:
//   1. Crystalline body — diffuse-lit yellowish tint from normal-mapped noise
//   2. Emissive veins   — ridged noise pattern with time-animated pulse
// ============================================================================

void ApplyEnergyMaterial(inout float3 color, float2 worldCell, float energyMask, float3 lightDir)
{
    if (energyMask <= 0.0)
        return;

    // ---- 1. Crystalline body diffuse --------------------------------------
    float2 eP = worldCell * 0.060;
    float e0 = fbmNoise(eP + float2(7.0, 241.0), 4);
    float enx = fbmNoise(eP + float2(9.0, 241.0), 4);
    float eny = fbmNoise(eP + float2(7.0, 243.0), 4);
    float2 eGrad = float2(enx - e0, eny - e0);
    float3 eNormal = normalize(float3(-eGrad * 6.0, 1.0));
    float eDiffuse = dot(eNormal, lightDir) * 0.5 + 0.5;

    // ---- 2. Emissive veins with animated pulse ----------------------------
    float veins = smoothstep(0.76, 0.95, 1.0 - abs(e0 * 2.0 - 1.0));
    float pulseE = MaterialEmissivePulse.y + MaterialEmissivePulse.z * (0.5 + 0.5 * sin(Time * MaterialEmissivePulse.x + e0 * 9.0));
    float3 energyTint = lerp(float3(0.88, 0.90, 0.66), float3(1.00, 1.00, 0.78), eDiffuse);

    color = lerp(color, color * energyTint * (0.94 + 0.14 * eDiffuse), energyMask * 0.85);
    color += MaterialEmissiveEnergy.rgb * (veins * MaterialEmissiveEnergy.a * pulseE * energyMask);
}
