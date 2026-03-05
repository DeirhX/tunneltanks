namespace Tunnerer.Desktop.Rendering.Dx11;

using ImGuiNET;
using Silk.NET.Direct3D11;
using Tunnerer.Desktop.Config;

public sealed unsafe partial class Backend
{
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
        cbData.UseTerrainAux = upload.TerrainAux != null ? 1f : 0f;
        cbData.BloomThreshold = DesktopScreenTweaks.PostBloomThreshold;
        cbData.BloomStrength = DesktopScreenTweaks.PostBloomStrength;
        cbData.BloomWeightCenter = DesktopScreenTweaks.PostBloomWeightCenter;
        cbData.BloomWeightAxis = DesktopScreenTweaks.PostBloomWeightAxis;
        cbData.BloomWeightDiagonal = DesktopScreenTweaks.PostBloomWeightDiagonal;
        cbData.VignetteStrength = DesktopScreenTweaks.PostVignetteStrength;
        cbData.EdgeLightStrength = DesktopScreenTweaks.PostTerrainEdgeLightStrength;
        cbData.EdgeLightBias = DesktopScreenTweaks.PostTerrainEdgeLightBias;
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
        cbData.VignetteInnerRadius = DesktopScreenTweaks.PostVignetteInnerRadius;
        cbData.VignetteOuterRadius = DesktopScreenTweaks.PostVignetteOuterRadius;
        cbData.Quality = (float)upload.Quality;
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
        cbData.NativeEdgeSoftness = DesktopScreenTweaks.NativeContinuousEdgeSoftness;
        cbData.NativeBoundaryBlend = DesktopScreenTweaks.NativeContinuousBoundaryBlend;
        cbData.NativeSampleFactor = upload.NativeSampleCount <= 1 ? 0f : upload.NativeSampleCount <= 2 ? 0.5f : 1f;
        cbData.NativePad = 0f;

        float lx = DesktopScreenTweaks.LightDirX, ly = DesktopScreenTweaks.LightDirY, lz = DesktopScreenTweaks.LightDirZ;
        float lLen = MathF.Sqrt(lx * lx + ly * ly + lz * lz);
        if (lLen > 0.001f) { lx /= lLen; ly /= lLen; lz /= lLen; }
        cbData.LightDirX = lx;
        cbData.LightDirY = ly;
        cbData.LightDirZ = lz;
        cbData.LightNormalStrength = DesktopScreenTweaks.LightNormalStrength;
        float hx = lx, hy = ly, hz = lz + 1f;
        float hLen = MathF.Sqrt(hx * hx + hy * hy + hz * hz);
        if (hLen > 0.001f) { hx /= hLen; hy /= hLen; hz /= hLen; }
        cbData.HalfVecX = hx;
        cbData.HalfVecY = hy;
        cbData.HalfVecZ = hz;
        cbData.LightMicroNormalStrength = DesktopScreenTweaks.LightMicroNormalStrength;
        cbData.LightAmbient = DesktopScreenTweaks.LightAmbient;
        cbData.LightDiffuseWeight = DesktopScreenTweaks.LightDiffuseWeight;
        cbData.LightShininess = DesktopScreenTweaks.LightShininess;
        cbData.LightSpecularIntensity = DesktopScreenTweaks.LightSpecularIntensity;

        int glowCount = Math.Clamp(upload.TankHeatGlowCount, 0, 8);
        cbData.TankGlowCount = glowCount;
        if (upload.TankHeatGlowData != null)
        {
            for (int i = 0; i < glowCount; i++)
            {
                int src = i * 4;
                int dst = i * 4;
                cbData.TankGlow[dst + 0] = upload.TankHeatGlowData[src + 0];
                cbData.TankGlow[dst + 1] = upload.TankHeatGlowData[src + 1];
                cbData.TankGlow[dst + 2] = upload.TankHeatGlowData[src + 2];
                cbData.TankGlow[dst + 3] = upload.TankHeatGlowData[src + 3];
            }
        }

        MappedSubresource mapped;
        if (_context->Map((ID3D11Resource*)_postParamsBuffer, 0, Silk.NET.Direct3D11.Map.WriteDiscard, 0, &mapped) >= 0)
        {
            *(PostParamsCBuffer*)mapped.PData = cbData;
            _context->Unmap((ID3D11Resource*)_postParamsBuffer, 0);
        }
    }

    #pragma warning disable CS0649
    private unsafe struct PostParamsCBuffer
    {
        public float TexelSizeX, TexelSizeY, PixelScale, Time;
        public float WorldSizeX, WorldSizeY, CameraPixelsX, CameraPixelsY;
        public float ViewSizeX, ViewSizeY, UseTerrainAux, BloomThreshold;
        public float BloomStrength, BloomWeightCenter, BloomWeightAxis, BloomWeightDiagonal;
        public float VignetteStrength, EdgeLightStrength, EdgeLightBias, _padEdgeAlign0;
        public float TankHeatGlowR, TankHeatGlowG, TankHeatGlowB, TankHeatGlowA;
        public float TerrainHeatGlowR, TerrainHeatGlowG, TerrainHeatGlowB, TerrainHeatThreshold;
        public float TerrainMaskEdgeStrength, TerrainMaskCaveDarken, TerrainMaskSolidLift, TerrainMaskOutlineDarken;
        public float TerrainMaskRimLift, TerrainMaskBoundaryScale, VignetteInnerRadius, VignetteOuterRadius;
        public float Quality, _padQuality0, _padQuality1, _padQuality2;
        public float MaterialEnergyR, MaterialEnergyG, MaterialEnergyB, MaterialEnergyStrength;
        public float MaterialScorchedR, MaterialScorchedG, MaterialScorchedB, MaterialScorchedStrength;
        public float MaterialPulseFreq, MaterialPulseMin, MaterialPulseRange, MaterialPulsePad;
        public float NativeEdgeSoftness, NativeBoundaryBlend, NativeSampleFactor, NativePad;
        public float LightDirX, LightDirY, LightDirZ, LightNormalStrength;
        public float HalfVecX, HalfVecY, HalfVecZ, LightMicroNormalStrength;
        public float LightAmbient, LightDiffuseWeight, LightShininess, LightSpecularIntensity;
        public float TankGlowCount, _pad0, _pad1, _pad2;
        public fixed float TankGlow[32];
    }
    #pragma warning restore CS0649
}
