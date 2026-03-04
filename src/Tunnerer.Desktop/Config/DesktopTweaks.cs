namespace Tunnerer.Desktop.Config;

using Tunnerer.Core.Types;

public enum RenderBackendKind
{
    Dx11 = 0,
    Dx12 = 1,
}

public static class DesktopTweaks
{
    public const RenderBackendKind DefaultRenderBackend = RenderBackendKind.Dx11;
}

public static class DesktopScreenTweaks
{
    public static readonly Size WindowSize = new(1920, 1200);
    public static readonly Size RenderSurfaceSize = new(320, 200);
    public const int PixelScale = 6;
    public const int NativeContinuousSampleLow = 1;
    public const int NativeContinuousSampleMedium = 2;
    public const int NativeContinuousSampleHigh = 4;
    public const float NativeContinuousEdgeSoftness = 0.10f;
    public const float NativeContinuousBoundaryBlend = 0.75f;
    public const float NativeContinuousRenderBudgetMs = 5.0f;
    public const int NativeContinuousBudgetHysteresisFrames = 10;
    public const float NativeContinuousBudgetUnderThreshold = 0.65f;
    public const int NativeContinuousRecoveryFramesMultiplier = 20;
    public const int HeatAuxUpdateIntervalFrames = 2;

    public const float PostBloomThreshold = 0.72f;
    public const float PostBloomStrength = 0.60f;
    public const float PostBloomWeightCenter = 0.30f;
    public const float PostBloomWeightAxis = 0.11f;
    public const float PostBloomWeightDiagonal = 0.07f;
    public const float PostVignetteStrength = 0.18f;
    public const float PostVignetteInnerRadius = 0.35f;
    public const float PostVignetteOuterRadius = 0.95f;
    public const float PostTerrainEdgeLightStrength = 0.10f;
    public const float PostTerrainEdgeLightBias = 0.05f;
    public const float PostTerrainHeatThreshold = 0.10f;
    public const float PostTerrainMaskEdgeStrength = 0.18f;
    public const float PostTerrainMaskCaveDarken = 0.12f;
    public const float PostTerrainMaskSolidLift = 0.04f;
    public const float PostTerrainMaskOutlineDarken = 0.26f;
    public const float PostTerrainMaskRimLift = 0.07f;
    public const float PostTerrainMaskBoundaryScale = 2.4f;
    public const float PostMaterialEmissiveEnergyR = 0.7843f;
    public const float PostMaterialEmissiveEnergyG = 0.8627f;
    public const float PostMaterialEmissiveEnergyB = 0.2353f;
    public const float PostMaterialEmissiveScorchedR = 0.7059f;
    public const float PostMaterialEmissiveScorchedG = 0.3137f;
    public const float PostMaterialEmissiveScorchedB = 0.1176f;
    public const float PostMaterialEmissiveEnergyStrength = 0.95f;
    public const float PostMaterialEmissiveScorchedStrength = 0.32f;
    public const float PostMaterialEmissivePulseFreq = 3.0f;
    public const float PostMaterialEmissivePulseMin = 0.8f;
    public const float PostMaterialEmissivePulseRange = 0.2f;
    public const float PostTankHeatGlowR = 0.78f;
    public const float PostTankHeatGlowG = 0.24f;
    public const float PostTankHeatGlowB = 0.04f;
    public const float PostTankHeatGlowMinHeat = 1.5f;
    public const float PostTankHeatGlowBaseRadius = 2.5f;
    public const float PostTankHeatGlowScaleRadius = 2.5f;
    public const float PostTerrainHeatGlowR = 1.0f;
    public const float PostTerrainHeatGlowG = 0.4500f;
    public const float PostTerrainHeatGlowB = 0.0500f;
    public const byte PostEmissiveEnergyLow = 150;
    public const byte PostEmissiveEnergyMedium = 190;
    public const byte PostEmissiveEnergyHigh = 230;
    public const byte PostEmissiveScorchedHigh = 64;
    public const byte PostEmissiveScorchedLow = 32;
    public const float LightDirX = -0.35f;
    public const float LightDirY = -0.55f;
    public const float LightDirZ = 0.70f;
    public const float LightAmbient = 0.22f;
    public const float LightDiffuseWeight = 0.68f;
    public const float LightNormalStrength = 2.2f;
    public const float LightShininess = 8f;
    public const float LightSpecularIntensity = 0.10f;
    public const float LightMicroNormalStrength = 0.35f;
}
