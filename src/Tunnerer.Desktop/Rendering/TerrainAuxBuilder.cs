namespace Tunnerer.Desktop.Rendering;

using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;

public sealed class TerrainAuxBuilder
{
    private readonly TerrainBlurField _gpuBlurField = new();

    public int BuildGpuTankHeatGlowData(
        IReadOnlyList<Tank> tanks,
        in RenderView view,
        float[] target)
    {
        int camPixelX = view.CameraPixels.X;
        int camPixelY = view.CameraPixels.Y;
        int pixelScale = view.PixelScale;
        int targetW = view.ViewSize.X;
        int targetH = view.ViewSize.Y;
        int count = 0;
        for (int i = 0; i < tanks.Count && count < Tweaks.World.MaxPlayers; i++)
        {
            Tank tank = tanks[i];
            if (tank.IsDead) continue;

            float t = tank.Heat / Tweaks.Tank.HeatMax;
            float minVisible = DesktopScreenTweaks.PostTankHeatGlowMinHeat / Tweaks.Tank.HeatMax;
            float visibleAtHeat = 10f / Tweaks.Tank.HeatMax;
            float damageStart = Tweaks.Tank.HeatSafeMax / Tweaks.Tank.HeatMax;
            float earlyRange = Math.Max(0.0001f, visibleAtHeat - minVisible);
            float lateRange = Math.Max(0.0001f, damageStart - visibleAtHeat);
            float earlyT = Math.Clamp((t - minVisible) / earlyRange, 0.0f, 1.0f);
            float lateT = Math.Clamp((t - visibleAtHeat) / lateRange, 0.0f, 1.0f);
            float visibleT = Math.Clamp(0.65f * MathF.Sqrt(earlyT) + 0.35f * lateT, 0.0f, 1.0f);
            if (tank.Reactor.Health < tank.Reactor.HealthCapacity)
            {
                float damageFrac = 1.0f - (float)tank.Reactor.Health / Math.Max(1f, tank.Reactor.HealthCapacity);
                float damageVisual = 0.85f + 0.15f * MathF.Sqrt(Math.Clamp(damageFrac * 8.0f, 0.0f, 1.0f));
                visibleT = Math.Max(visibleT, damageVisual);
            }
            if (visibleT <= 0.0f) continue;

            float radiusFactor = (0.55f + 0.45f * MathF.Sqrt(visibleT)) * 1.35f;
            float glowRadiusPx = pixelScale * (DesktopScreenTweaks.PostTankHeatGlowBaseRadius + DesktopScreenTweaks.PostTankHeatGlowScaleRadius * radiusFactor);
            float cx = (tank.Position.X + 0.5f) * pixelScale - camPixelX;
            float cy = (tank.Position.Y + 0.5f) * pixelScale - camPixelY;

            if (cx + glowRadiusPx < 0 || cy + glowRadiusPx < 0 || cx - glowRadiusPx >= targetW || cy - glowRadiusPx >= targetH)
                continue;

            int baseIdx = count * 4;
            target[baseIdx + 0] = cx / targetW;
            target[baseIdx + 1] = cy / targetH;
            target[baseIdx + 2] = glowRadiusPx / MathF.Max(targetW, targetH);
            // Store normalized heat visibility; shader derives glow/distortion from this directly.
            target[baseIdx + 3] = visibleT;
            count++;
        }

        return count;
    }

    public void BuildGpuTerrainAuxData(TerrainGrid terrain, byte[] target)
    {
        _gpuBlurField.Rebuild(terrain);
        int w = terrain.Width;
        int len = terrain.Size.Area;
        for (int i = 0; i < len; i++)
        {
            int x = i % w;
            int y = i / w;
            WriteTerrainAux(terrain, i, terrain.GetPixelRaw(i), _gpuBlurField.SampleAsByte(x, y), target, i * 4);
        }
    }

    public void BuildGpuTerrainAuxRect(TerrainGrid terrain, byte[] target, in Rect dirtyRect)
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

    public static void UpdateGpuTerrainAuxHeatOnly(TerrainGrid terrain, byte[] target, in Rect dirtyRect)
    {
        RectMath.GetMinMaxInclusive(dirtyRect, out int minX, out int minY, out int maxX, out int maxY);
        int w = terrain.Width;
        int rowCount = maxY - minY + 1;

        const float scale = TerrainGrid.HeatByteScale;
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

    private static void WriteTerrainAux(TerrainGrid terrain, int idx, TerrainPixel pixel, byte sdfValue, byte[] target, int writeIndex)
    {
        // Aux channel layout:
        // R = heat, G = smoothed SDF, B = material class, A = scorch damage level.
        const byte materialNone = 0;
        const byte materialDirt = 85;
        const byte materialStone = 170;
        const byte materialEnergy = 212;
        const byte materialBase = 255;

        byte material = materialNone;
        if (pixel == TerrainPixel.DirtGrow || Pixel.IsDirt(pixel))
            material = materialDirt;
        else if (Pixel.IsEnergy(pixel))
            material = materialEnergy;
        else if (pixel == TerrainPixel.BaseBarrier ||
                 pixel == TerrainPixel.BaseCore ||
                 Pixel.IsBase(pixel))
            material = materialBase;
        else if (Pixel.IsRock(pixel) || Pixel.IsConcrete(pixel) ||
                 Pixel.IsMineral(pixel) || Pixel.IsBlockingCollision(pixel))
            material = materialStone;

        byte scorch = 0;
        switch (pixel)
        {
            case TerrainPixel.DecalHigh:
                scorch = DesktopScreenTweaks.PostEmissiveScorchedHigh;
                break;
            case TerrainPixel.DecalLow:
                scorch = DesktopScreenTweaks.PostEmissiveScorchedLow;
                break;
        }

        float heat = terrain.GetHeatTemperature(idx);
        target[writeIndex] = (byte)Math.Clamp((int)MathF.Round(heat / TerrainGrid.HeatByteScale), 0, 255);
        target[writeIndex + 1] = sdfValue;
        target[writeIndex + 2] = material;
        target[writeIndex + 3] = scorch;
    }
}
