namespace Tunnerer.Desktop.Rendering.Dx11;

using ImGuiNET;
using Silk.NET.Direct3D11;
using Tunnerer.Desktop.Config;

public sealed unsafe partial class Backend
{
    private const float PostParamsLayoutVersionValue = 3.0f;

    private void UpdatePostParamsBuffer(in GamePixelsUpload upload)
    {
        if (_postParamsBuffer == null)
            return;
        PostParamsCBuffer cbData = default;
        cbData.TexelSizeX = 1f / Math.Max(1, upload.View.ViewSize.X);
        cbData.TexelSizeY = 1f / Math.Max(1, upload.View.ViewSize.Y);
        cbData.PixelScale = upload.View.PixelScale;
        cbData.Time = (float)ImGui.GetTime();
        cbData.WorldSizeX = upload.View.WorldSize.X;
        cbData.WorldSizeY = upload.View.WorldSize.Y;
        cbData.CameraPixelsX = upload.View.CameraPixels.X;
        cbData.CameraPixelsY = upload.View.CameraPixels.Y;
        cbData.ViewSizeX = upload.View.ViewSize.X;
        cbData.ViewSizeY = upload.View.ViewSize.Y;
        cbData.UseTerrainAux = BoolToFloat(upload.TerrainAux.Data != null);
        cbData.PostParamsLayoutVersion = PostParamsLayoutVersionValue;
        cbData.HeatDebugOverlay = BoolToFloat(upload.PostProcess.HeatDebugOverlayEnabled);
        cbData.PostVignetteEnabled = BoolToFloat(HasPass(upload.PostProcess.PassFlags, PostProcessPassFlags.Vignette));
        cbData.PostTerrainCurveEnabled = BoolToFloat(HasPass(upload.PostProcess.PassFlags, PostProcessPassFlags.TerrainCurve));
        cbData.PostTerrainAuxEnabled = BoolToFloat(HasPass(upload.PostProcess.PassFlags, PostProcessPassFlags.TerrainAux));
        cbData.PostTankGlowEnabled = BoolToFloat(HasPass(upload.PostProcess.PassFlags, PostProcessPassFlags.TankGlow));
        cbData.PostTerrainHeatEnabled = BoolToFloat(HasPass(upload.PostProcess.PassFlags, PostProcessPassFlags.TerrainHeat));
        cbData.PostThermalRegionsEnabled = BoolToFloat(HasPass(upload.PostProcess.PassFlags, PostProcessPassFlags.ThermalRegions));
        cbData.ThermalTileSizeCells = upload.PostProcess.ThermalRegions.TileSizeCells;
        cbData.ThermalTileHeatThreshold01 = upload.PostProcess.ThermalRegions.ActiveThresholdHeat01;
        cbData.VignetteStrength = DesktopScreenTweaks.PostVignetteStrength;
        cbData.VignetteInnerRadius = DesktopScreenTweaks.PostVignetteInnerRadius;
        cbData.VignetteOuterRadius = DesktopScreenTweaks.PostVignetteOuterRadius;
        cbData.TankHeatGlowR = DesktopScreenTweaks.PostTankHeatGlowR;
        cbData.TankHeatGlowG = DesktopScreenTweaks.PostTankHeatGlowG;
        cbData.TankHeatGlowB = DesktopScreenTweaks.PostTankHeatGlowB;
        cbData.TankHeatGlowA = DesktopScreenTweaks.PostTankHeatDistortionEnabled ? 1f : 0f;
        cbData.TerrainHeatGlowR = DesktopScreenTweaks.PostTerrainHeatGlowR;
        cbData.TerrainHeatGlowG = DesktopScreenTweaks.PostTerrainHeatGlowG;
        cbData.TerrainHeatGlowB = DesktopScreenTweaks.PostTerrainHeatGlowB;
        cbData.TerrainHeatThreshold = DesktopScreenTweaks.PostTerrainHeatThreshold;
        cbData.TerrainMaskEdgeStrength = DesktopScreenTweaks.PostTerrainMaskEdgeStrength;
        cbData.TerrainMaskCaveDarken = DesktopScreenTweaks.PostTerrainMaskCaveDarken;
        cbData.TerrainMaskSolidLift = DesktopScreenTweaks.PostTerrainMaskSolidLift;
        cbData.TerrainMaskOutlineDarken = DesktopScreenTweaks.PostTerrainMaskOutlineDarken;
        cbData.TerrainMaskRimLift = DesktopScreenTweaks.PostTerrainMaskRimLift;
        cbData.TerrainMaskBoundaryScale = DesktopScreenTweaks.PostTerrainMaskBoundaryScale;
        cbData.Quality = (float)upload.PostProcess.Quality;
        cbData.MaterialEnergyR = DesktopScreenTweaks.PostMaterialEmissiveEnergyR;
        cbData.MaterialEnergyG = DesktopScreenTweaks.PostMaterialEmissiveEnergyG;
        cbData.MaterialEnergyB = DesktopScreenTweaks.PostMaterialEmissiveEnergyB;
        cbData.MaterialEnergyStrength = DesktopScreenTweaks.PostMaterialEmissiveEnergyStrength;
        cbData.MaterialScorchedR = DesktopScreenTweaks.PostMaterialEmissiveScorchedR;
        cbData.MaterialScorchedG = DesktopScreenTweaks.PostMaterialEmissiveScorchedG;
        cbData.MaterialScorchedB = DesktopScreenTweaks.PostMaterialEmissiveScorchedB;
        cbData.MaterialScorchedStrength = DesktopScreenTweaks.PostMaterialEmissiveScorchedStrength;
        cbData.MaterialPulseFreq = DesktopScreenTweaks.PostMaterialEmissivePulseFreq;
        cbData.MaterialPulseMin = DesktopScreenTweaks.PostMaterialEmissivePulseMin;
        cbData.MaterialPulseRange = DesktopScreenTweaks.PostMaterialEmissivePulseRange;
        cbData.MaterialPulsePad = 0f;
        bool nativeSmoothingEnabled = HasPass(upload.PostProcess.PassFlags, PostProcessPassFlags.NativeTerrainSmoothing);
        cbData.NativeEdgeSoftness = nativeSmoothingEnabled ? DesktopScreenTweaks.NativeContinuousEdgeSoftness : 0f;
        cbData.NativeBoundaryBlend = nativeSmoothingEnabled ? DesktopScreenTweaks.NativeContinuousBoundaryBlend : 0f;
        cbData.NativeSampleFactor = upload.NativeContinuous.SampleCount <= 1
            ? 0f
            : upload.NativeContinuous.SampleCount <= 2 ? 0.5f : 1f;
        cbData.NativeCurvingEnabled = BoolToFloat(HasPass(upload.PostProcess.PassFlags, PostProcessPassFlags.NativeEdgeCurving));

        float lx = DesktopScreenTweaks.LightDirX, ly = DesktopScreenTweaks.LightDirY, lz = DesktopScreenTweaks.LightDirZ;
        Normalize3(ref lx, ref ly, ref lz);
        cbData.LightDirX = lx;
        cbData.LightDirY = ly;
        cbData.LightDirZ = lz;
        cbData.LightNormalStrength = DesktopScreenTweaks.LightNormalStrength;
        float hx = lx, hy = ly, hz = lz + 1f;
        Normalize3(ref hx, ref hy, ref hz);
        cbData.HalfVecX = hx;
        cbData.HalfVecY = hy;
        cbData.HalfVecZ = hz;
        cbData.LightMicroNormalStrength = DesktopScreenTweaks.LightMicroNormalStrength;
        cbData.LightAmbient = DesktopScreenTweaks.LightAmbient;
        cbData.LightDiffuseWeight = DesktopScreenTweaks.LightDiffuseWeight;
        cbData.LightShininess = DesktopScreenTweaks.LightShininess;
        cbData.LightSpecularIntensity = DesktopScreenTweaks.LightSpecularIntensity;

        int glowCount = Math.Clamp(upload.TankGlow.Count, 0, 8);
        cbData.TankGlowCount = glowCount;
        if (upload.TankGlow.Data != null)
        {
            for (int i = 0; i < glowCount; i++)
            {
                int src = i * 4;
                int dst = i * 4;
                cbData.TankGlow[dst + 0] = upload.TankGlow.Data[src + 0];
                cbData.TankGlow[dst + 1] = upload.TankGlow.Data[src + 1];
                cbData.TankGlow[dst + 2] = upload.TankGlow.Data[src + 2];
                cbData.TankGlow[dst + 3] = upload.TankGlow.Data[src + 3];
            }
        }

        MappedSubresource mapped;
        if (_context->Map((ID3D11Resource*)_postParamsBuffer, 0, Silk.NET.Direct3D11.Map.WriteDiscard, 0, &mapped) >= 0)
        {
            *(PostParamsCBuffer*)mapped.PData = cbData;
            _context->Unmap((ID3D11Resource*)_postParamsBuffer, 0);
        }
    }

    private static float BoolToFloat(bool value) => value ? 1f : 0f;

    private static bool HasPass(PostProcessPassFlags flags, PostProcessPassFlags pass) =>
        (flags & pass) != 0;

    private static void Normalize3(ref float x, ref float y, ref float z)
    {
        float len = MathF.Sqrt(x * x + y * y + z * z);
        if (len > 0.001f)
        {
            x /= len;
            y /= len;
            z /= len;
        }
    }

    #pragma warning disable CS0649
    private unsafe struct PostParamsCBuffer
    {
        public float TexelSizeX, TexelSizeY, PixelScale, Time;
        public float WorldSizeX, WorldSizeY, CameraPixelsX, CameraPixelsY;
        public float ViewSizeX, ViewSizeY, UseTerrainAux, PostParamsLayoutVersion;
        public float HeatDebugOverlay, PostVignetteEnabled, PostTerrainCurveEnabled, PostTerrainAuxEnabled;
        public float PostTankGlowEnabled, PostTerrainHeatEnabled, PostThermalRegionsEnabled, ThermalTileSizeCells;
        public float ThermalTileHeatThreshold01, _padOverlay0, VignetteStrength, VignetteInnerRadius;
        public float VignetteOuterRadius, TerrainMaskEdgeStrength, TerrainMaskCaveDarken, TerrainMaskSolidLift;
        public float TerrainMaskOutlineDarken, TerrainMaskRimLift, TerrainMaskBoundaryScale, Quality;
        public float _padQuality0, MaterialEnergyR, MaterialEnergyG, MaterialEnergyB;
        public float MaterialEnergyStrength, MaterialScorchedR, MaterialScorchedG, MaterialScorchedB;
        public float MaterialScorchedStrength, MaterialPulseFreq, MaterialPulseMin, MaterialPulseRange;
        public float MaterialPulsePad, NativeEdgeSoftness, NativeBoundaryBlend, NativeSampleFactor;
        public float NativeCurvingEnabled, LightDirX, LightDirY, LightDirZ;
        public float LightNormalStrength, HalfVecX, HalfVecY, HalfVecZ;
        public float LightMicroNormalStrength, LightAmbient, LightDiffuseWeight, LightShininess;
        public float LightSpecularIntensity, TankHeatGlowR, TankHeatGlowG, TankHeatGlowB;
        public float TankHeatGlowA, TerrainHeatGlowR, TerrainHeatGlowG, TerrainHeatGlowB;
        public float TerrainHeatThreshold, TankGlowCount, _pad0, _pad1;
        public fixed float TankGlow[32];
    }
    #pragma warning restore CS0649
}
