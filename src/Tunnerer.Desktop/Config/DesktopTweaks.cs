namespace Tunnerer.Desktop.Config;

using Tunnerer.Core.Config;
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
    public static Size WindowSize => Tweaks.Screen.WindowSize;
    public static Size RenderSurfaceSize => Tweaks.Screen.RenderSurfaceSize;
    public static int PixelScale => Tweaks.Screen.PixelScale;
    public static int NativeContinuousSampleLow => Tweaks.Screen.NativeContinuousSampleLow;
    public static int NativeContinuousSampleMedium => Tweaks.Screen.NativeContinuousSampleMedium;
    public static int NativeContinuousSampleHigh => Tweaks.Screen.NativeContinuousSampleHigh;
    public static float NativeContinuousEdgeSoftness => Tweaks.Screen.NativeContinuousEdgeSoftness;
    public static float NativeContinuousBoundaryBlend => Tweaks.Screen.NativeContinuousBoundaryBlend;
    public static float NativeContinuousRenderBudgetMs => Tweaks.Screen.NativeContinuousRenderBudgetMs;
    public static int NativeContinuousBudgetHysteresisFrames => Tweaks.Screen.NativeContinuousBudgetHysteresisFrames;
    public static float NativeContinuousBudgetUnderThreshold => Tweaks.Screen.NativeContinuousBudgetUnderThreshold;
    public static int NativeContinuousRecoveryFramesMultiplier => Tweaks.Screen.NativeContinuousRecoveryFramesMultiplier;
    public const int HeatAuxUpdateIntervalFrames = 2;

    public static float PostBloomThreshold => Tweaks.Screen.PostBloomThreshold;
    public static float PostBloomStrength => Tweaks.Screen.PostBloomStrength;
    public static float PostBloomWeightCenter => Tweaks.Screen.PostBloomWeightCenter;
    public static float PostBloomWeightAxis => Tweaks.Screen.PostBloomWeightAxis;
    public static float PostBloomWeightDiagonal => Tweaks.Screen.PostBloomWeightDiagonal;
    public static float PostVignetteStrength => Tweaks.Screen.PostVignetteStrength;
    public static float PostVignetteInnerRadius => Tweaks.Screen.PostVignetteInnerRadius;
    public static float PostVignetteOuterRadius => Tweaks.Screen.PostVignetteOuterRadius;
    public static float PostTerrainEdgeLightStrength => Tweaks.Screen.PostTerrainEdgeLightStrength;
    public static float PostTerrainEdgeLightBias => Tweaks.Screen.PostTerrainEdgeLightBias;
    public static float PostTerrainHeatThreshold => Tweaks.Screen.PostTerrainHeatThreshold;
    public static float PostTerrainMaskEdgeStrength => Tweaks.Screen.PostTerrainMaskEdgeStrength;
    public static float PostTerrainMaskCaveDarken => Tweaks.Screen.PostTerrainMaskCaveDarken;
    public static float PostTerrainMaskSolidLift => Tweaks.Screen.PostTerrainMaskSolidLift;
    public static float PostTerrainMaskOutlineDarken => Tweaks.Screen.PostTerrainMaskOutlineDarken;
    public static float PostTerrainMaskRimLift => Tweaks.Screen.PostTerrainMaskRimLift;
    public static float PostTerrainMaskBoundaryScale => Tweaks.Screen.PostTerrainMaskBoundaryScale;
    public static float PostMaterialEmissiveEnergyR => Tweaks.Screen.PostMaterialEmissiveEnergyR;
    public static float PostMaterialEmissiveEnergyG => Tweaks.Screen.PostMaterialEmissiveEnergyG;
    public static float PostMaterialEmissiveEnergyB => Tweaks.Screen.PostMaterialEmissiveEnergyB;
    public static float PostMaterialEmissiveScorchedR => Tweaks.Screen.PostMaterialEmissiveScorchedR;
    public static float PostMaterialEmissiveScorchedG => Tweaks.Screen.PostMaterialEmissiveScorchedG;
    public static float PostMaterialEmissiveScorchedB => Tweaks.Screen.PostMaterialEmissiveScorchedB;
    public static float PostMaterialEmissiveEnergyStrength => Tweaks.Screen.PostMaterialEmissiveEnergyStrength;
    public static float PostMaterialEmissiveScorchedStrength => Tweaks.Screen.PostMaterialEmissiveScorchedStrength;
    public static float PostMaterialEmissivePulseFreq => Tweaks.Screen.PostMaterialEmissivePulseFreq;
    public static float PostMaterialEmissivePulseMin => Tweaks.Screen.PostMaterialEmissivePulseMin;
    public static float PostMaterialEmissivePulseRange => Tweaks.Screen.PostMaterialEmissivePulseRange;
    public static float PostTankHeatGlowR => Tweaks.Screen.PostTankHeatGlowR;
    public static float PostTankHeatGlowG => Tweaks.Screen.PostTankHeatGlowG;
    public static float PostTankHeatGlowB => Tweaks.Screen.PostTankHeatGlowB;
    public static float PostTankHeatGlowMinHeat => Tweaks.Screen.PostTankHeatGlowMinHeat;
    public static float PostTankHeatGlowBaseRadius => Tweaks.Screen.PostTankHeatGlowBaseRadius;
    public static float PostTankHeatGlowScaleRadius => Tweaks.Screen.PostTankHeatGlowScaleRadius;
    public static float PostTerrainHeatGlowR => Tweaks.Screen.PostTerrainHeatGlowR;
    public static float PostTerrainHeatGlowG => Tweaks.Screen.PostTerrainHeatGlowG;
    public static float PostTerrainHeatGlowB => Tweaks.Screen.PostTerrainHeatGlowB;
    public static byte PostEmissiveEnergyLow => Tweaks.Screen.PostEmissiveEnergyLow;
    public static byte PostEmissiveEnergyMedium => Tweaks.Screen.PostEmissiveEnergyMedium;
    public static byte PostEmissiveEnergyHigh => Tweaks.Screen.PostEmissiveEnergyHigh;
    public static byte PostEmissiveScorchedHigh => Tweaks.Screen.PostEmissiveScorchedHigh;
    public static byte PostEmissiveScorchedLow => Tweaks.Screen.PostEmissiveScorchedLow;
    public static float LightDirX => Tweaks.Screen.LightDirX;
    public static float LightDirY => Tweaks.Screen.LightDirY;
    public static float LightDirZ => Tweaks.Screen.LightDirZ;
    public static float LightAmbient => Tweaks.Screen.LightAmbient;
    public static float LightDiffuseWeight => Tweaks.Screen.LightDiffuseWeight;
    public static float LightNormalStrength => Tweaks.Screen.LightNormalStrength;
    public static float LightShininess => Tweaks.Screen.LightShininess;
    public static float LightSpecularIntensity => Tweaks.Screen.LightSpecularIntensity;
    public static float LightMicroNormalStrength => Tweaks.Screen.LightMicroNormalStrength;
}
