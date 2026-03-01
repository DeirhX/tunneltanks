namespace Tunnerer.Desktop.Rendering.Dx11;

using Silk.NET.SDL;
using Tunnerer.Core.Config;
using Tunnerer.Desktop.Rendering;

public sealed unsafe partial class Backend
{
    private void EnsureProcessedPixels(int count)
    {
        if (_processedPixels.Length != count)
            _processedPixels = new uint[count];
    }

    private static void ApplyFallbackEffects(uint[] pixels, in GamePixelsUpload upload)
    {
        ApplyTerrainAuxLighting(pixels, upload);
        ApplyTankHeatGlow(pixels, upload);
    }

    private static void ApplyTerrainAuxLighting(uint[] pixels, in GamePixelsUpload upload)
    {
        var aux = upload.TerrainAux;
        int worldW = upload.View.WorldSize.X;
        int worldH = upload.View.WorldSize.Y;
        int viewW = upload.View.ViewSize.X;
        int viewH = upload.View.ViewSize.Y;
        int scale = upload.View.PixelScale;
        if (aux == null || worldW <= 0 || worldH <= 0 || scale <= 0)
            return;

        int camX = upload.View.CameraPixels.X;
        int camY = upload.View.CameraPixels.Y;
        float threshold = Tweaks.Screen.PostTerrainHeatThreshold;
        int glowR = (int)(Tweaks.Screen.PostTerrainHeatGlowR * 255f);
        int glowG = (int)(Tweaks.Screen.PostTerrainHeatGlowG * 255f);
        int glowB = (int)(Tweaks.Screen.PostTerrainHeatGlowB * 255f);
        float pulseTime = (Environment.TickCount64 & 0x7FFFFFFF) * 0.001f;
        float pulse = Tweaks.Screen.PostMaterialEmissivePulseMin +
                      Tweaks.Screen.PostMaterialEmissivePulseRange *
                      (0.5f + 0.5f * MathF.Sin(pulseTime * Tweaks.Screen.PostMaterialEmissivePulseFreq));
        int energyR = (int)(Tweaks.Screen.PostMaterialEmissiveEnergyR * 255f);
        int energyG = (int)(Tweaks.Screen.PostMaterialEmissiveEnergyG * 255f);
        int energyB = (int)(Tweaks.Screen.PostMaterialEmissiveEnergyB * 255f);
        int scorchedR = (int)(Tweaks.Screen.PostMaterialEmissiveScorchedR * 255f);
        int scorchedG = (int)(Tweaks.Screen.PostMaterialEmissiveScorchedG * 255f);
        int scorchedB = (int)(Tweaks.Screen.PostMaterialEmissiveScorchedB * 255f);

        for (int py = 0; py < viewH; py++)
        {
            int worldY = (py + camY) / scale;
            if ((uint)worldY >= (uint)worldH) continue;
            int row = py * viewW;
            int worldRow = worldY * worldW;
            for (int px = 0; px < viewW; px++)
            {
                int worldX = (px + camX) / scale;
                if ((uint)worldX >= (uint)worldW) continue;
                int auxIdx = (worldRow + worldX) * 4;
                float heat = aux[auxIdx] / 255f;
                byte energyByte = aux[auxIdx + 2];
                byte scorchedByte = aux[auxIdx + 3];

                int addR = 0, addG = 0, addB = 0;

                if (heat > threshold)
                {
                    float t = (heat - threshold) / MathF.Max(0.0001f, 1f - threshold);
                    addR += (int)(glowR * t * 0.35f);
                    addG += (int)(glowG * t * 0.35f);
                    addB += (int)(glowB * t * 0.35f);
                }
                if (energyByte > 0)
                {
                    float t = (energyByte / 255f) * Tweaks.Screen.PostMaterialEmissiveEnergyStrength * pulse;
                    addR += (int)(energyR * t);
                    addG += (int)(energyG * t);
                    addB += (int)(energyB * t);
                }
                if (scorchedByte > 0)
                {
                    float t = (scorchedByte / 255f) * Tweaks.Screen.PostMaterialEmissiveScorchedStrength * pulse;
                    addR += (int)(scorchedR * t);
                    addG += (int)(scorchedG * t);
                    addB += (int)(scorchedB * t);
                }
                if (addR != 0 || addG != 0 || addB != 0)
                    pixels[row + px] = Additive(pixels[row + px], addR, addG, addB);
            }
        }
    }

    private static void ApplyTankHeatGlow(uint[] pixels, in GamePixelsUpload upload)
    {
        var data = upload.TankHeatGlowData;
        int count = upload.TankHeatGlowCount;
        int viewW = upload.View.ViewSize.X;
        int viewH = upload.View.ViewSize.Y;
        if (data == null || count <= 0 || viewW <= 0 || viewH <= 0)
            return;

        float colorR = Tweaks.Screen.PostTankHeatGlowR * 255f;
        float colorG = Tweaks.Screen.PostTankHeatGlowG * 255f;
        float colorB = Tweaks.Screen.PostTankHeatGlowB * 255f;
        float maxDim = MathF.Max(viewW, viewH);

        for (int i = 0; i < count; i++)
        {
            int baseIdx = i * 4;
            float cx = data[baseIdx + 0] * viewW;
            float cy = data[baseIdx + 1] * viewH;
            float radius = data[baseIdx + 2] * maxDim;
            float intensity = Math.Clamp(data[baseIdx + 3], 0f, 1f);
            if (radius <= 0.01f || intensity <= 0.001f) continue;

            int minX = Math.Max(0, (int)MathF.Floor(cx - radius));
            int maxX = Math.Min(viewW - 1, (int)MathF.Ceiling(cx + radius));
            int minY = Math.Max(0, (int)MathF.Floor(cy - radius));
            int maxY = Math.Min(viewH - 1, (int)MathF.Ceiling(cy + radius));
            float invR = 1f / radius;

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * viewW;
                float dy = (y + 0.5f - cy) * invR;
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x + 0.5f - cx) * invR;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) continue;
                    float falloff = (1f - d) * (1f - d);
                    float a = falloff * intensity * 0.5f;
                    int idx = row + x;
                    pixels[idx] = Additive(pixels[idx], (int)(colorR * a), (int)(colorG * a), (int)(colorB * a));
                }
            }
        }
    }

    private void DrawCrosshairIntoPixels(uint[] pixels, int w, int h)
    {
        int mx, my;
        _sdl.GetMouseState(&mx, &my);
        int winW, winH;
        _sdl.GetWindowSize(_window, &winW, &winH);
        if (winW <= 0 || winH <= 0 || mx < 0 || my < 0 || mx >= winW || my >= winH)
            return;

        int px = mx * w / winW;
        int py = my * h / winH;

        const int arm = 10;
        const int gap = 3;
        const uint col = 0xFFFFFFFF;
        for (int i = gap; i <= arm; i++)
        {
            SetPixel(pixels, w, h, px - i, py, col);
            SetPixel(pixels, w, h, px + i, py, col);
            SetPixel(pixels, w, h, px, py - i, col);
            SetPixel(pixels, w, h, px, py + i, col);
        }
    }

    private static void SetPixel(uint[] buf, int w, int h, int x, int y, uint color)
    {
        if ((uint)x < (uint)w && (uint)y < (uint)h)
            buf[y * w + x] = color;
    }

    private static uint Additive(uint color, int addR, int addG, int addB)
    {
        int r = Math.Min(255, (int)((color >> 16) & 0xFF) + addR);
        int g = Math.Min(255, (int)((color >> 8) & 0xFF) + addG);
        int b = Math.Min(255, (int)(color & 0xFF) + addB);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }
}
