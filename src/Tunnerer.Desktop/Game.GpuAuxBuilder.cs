namespace Tunnerer.Desktop;

using Tunnerer.Core.Config;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;
using Tunnerer.Desktop.Rendering;

public partial class Game
{
    private int BuildGpuTankHeatGlowData(
        IReadOnlyList<Core.Entities.Tank> tanks,
        in RenderView view)
    {
        int camPixelX = view.CameraPixels.X;
        int camPixelY = view.CameraPixels.Y;
        int pixelScale = view.PixelScale;
        int targetW = view.ViewSize.X;
        int targetH = view.ViewSize.Y;
        int count = 0;
        for (int i = 0; i < tanks.Count && count < Tweaks.World.MaxPlayers; i++)
        {
            var tank = tanks[i];
            if (tank.IsDead) continue;

            float t = tank.Heat / Tweaks.Tank.HeatMax;
            float minVisible = DesktopScreenTweaks.PostTankHeatGlowMinHeat / Tweaks.Tank.HeatMax;
            float damageStart = Tweaks.Tank.HeatSafeMax / Tweaks.Tank.HeatMax;
            float visibleRange = Math.Max(0.0001f, damageStart - minVisible);
            float visibleT = Math.Clamp((t - minVisible) / visibleRange, 0.0f, 1.0f);
            if (tank.Reactor.Health < tank.Reactor.HealthCapacity)
            {
                float damageFrac = 1.0f - (float)tank.Reactor.Health / Math.Max(1f, tank.Reactor.HealthCapacity);
                float damageVisual = 0.85f + 0.15f * MathF.Sqrt(Math.Clamp(damageFrac * 8.0f, 0.0f, 1.0f));
                visibleT = Math.Max(visibleT, damageVisual);
            }
            if (visibleT <= 0.0f) continue;

            float intensity = visibleT * (0.35f + 2.8f * visibleT * visibleT);
            float radiusFactor = 0.45f + 0.55f * MathF.Sqrt(visibleT);
            float glowRadiusPx = pixelScale * (DesktopScreenTweaks.PostTankHeatGlowBaseRadius + DesktopScreenTweaks.PostTankHeatGlowScaleRadius * radiusFactor);
            float cx = (tank.Position.X + 0.5f) * pixelScale - camPixelX;
            float cy = (tank.Position.Y + 0.5f) * pixelScale - camPixelY;

            if (cx + glowRadiusPx < 0 || cy + glowRadiusPx < 0 || cx - glowRadiusPx >= targetW || cy - glowRadiusPx >= targetH)
                continue;

            int baseIdx = count * 4;
            _gpuTankHeatGlow[baseIdx + 0] = cx / targetW;
            _gpuTankHeatGlow[baseIdx + 1] = cy / targetH;
            _gpuTankHeatGlow[baseIdx + 2] = glowRadiusPx / MathF.Max(targetW, targetH);
            _gpuTankHeatGlow[baseIdx + 3] = intensity;
            count++;
        }

        return count;
    }

    private void BuildGpuTerrainAuxData(Core.Terrain.TerrainGrid terrain, byte[] target)
    {
        _gpuBlurField.Rebuild(terrain);
        int w = terrain.Width;
        int len = terrain.Size.Area;
        for (int i = 0; i < len; i++)
        {
            int x = i % w, y = i / w;
            WriteTerrainAux(terrain, i, terrain.GetPixelRaw(i), _gpuBlurField.SampleAsByte(x, y), target, i * 4);
        }
    }

    private void BuildGpuTerrainAuxRect(Core.Terrain.TerrainGrid terrain, byte[] target, in Rect dirtyRect)
    {
        RectMath.GetMinMaxInclusive(dirtyRect, out int minX, out int minY, out int maxX, out int maxY);
        _gpuBlurField.UpdateRect(terrain, minX, minY, maxX, maxY);
        int w = terrain.Width;
        for (int y = minY; y <= maxY; y++)
        {
            int row = y * w;
            for (int x = minX; x <= maxX; x++)
            {
                int idx = row + x;
                WriteTerrainAux(terrain, idx, terrain.GetPixelRaw(idx), _gpuBlurField.SampleAsByte(x, y), target, idx * 4);
            }
        }
    }

    private static void UpdateGpuTerrainAuxHeatOnly(Core.Terrain.TerrainGrid terrain, byte[] target, in Rect dirtyRect)
    {
        RectMath.GetMinMaxInclusive(dirtyRect, out int minX, out int minY, out int maxX, out int maxY);
        int w = terrain.Width;
        int rowCount = maxY - minY + 1;

        const float scale = Core.Terrain.TerrainGrid.HeatByteScale;
        if (rowCount >= 32)
        {
            Parallel.For(minY, maxY + 1, y =>
            {
                int row = y * w;
                for (int x = minX; x <= maxX; x++)
                {
                    int idx = row + x;
                    float heat = terrain.GetHeatTemperature(idx);
                    target[idx * 4] = (byte)Math.Clamp((int)MathF.Round(heat / scale), 0, 255);
                }
            });
        }
        else
        {
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * w;
                for (int x = minX; x <= maxX; x++)
                {
                    int idx = row + x;
                    float heat = terrain.GetHeatTemperature(idx);
                    target[idx * 4] = (byte)Math.Clamp((int)MathF.Round(heat / scale), 0, 255);
                }
            }
        }
    }

    private static void WriteTerrainAux(Core.Terrain.TerrainGrid terrain, int idx, Core.Terrain.TerrainPixel pixel, byte sdfValue, byte[] target, int writeIndex)
    {
        byte energy = 0;
        byte scorched = 0;
        switch (pixel)
        {
            case Core.Terrain.TerrainPixel.EnergyLow:
                energy = DesktopScreenTweaks.PostEmissiveEnergyLow;
                break;
            case Core.Terrain.TerrainPixel.EnergyMedium:
                energy = DesktopScreenTweaks.PostEmissiveEnergyMedium;
                break;
            case Core.Terrain.TerrainPixel.EnergyHigh:
                energy = DesktopScreenTweaks.PostEmissiveEnergyHigh;
                break;
            case Core.Terrain.TerrainPixel.DecalHigh:
                scorched = DesktopScreenTweaks.PostEmissiveScorchedHigh;
                break;
            case Core.Terrain.TerrainPixel.DecalLow:
                scorched = DesktopScreenTweaks.PostEmissiveScorchedLow;
                break;
        }

        float heat = terrain.GetHeatTemperature(idx);
        target[writeIndex] = (byte)Math.Clamp((int)MathF.Round(heat / Core.Terrain.TerrainGrid.HeatByteScale), 0, 255);
        target[writeIndex + 1] = sdfValue;
        target[writeIndex + 2] = energy;
        target[writeIndex + 3] = scorched;
    }
}
