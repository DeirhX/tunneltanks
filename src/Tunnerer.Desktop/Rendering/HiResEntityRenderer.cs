using Tunnerer.Core.Entities;
using Tunnerer.Core.Config;

namespace Tunnerer.Desktop.Rendering;

public sealed class HiResEntityRenderer
{
    private const float ShadowDarken = 0.82f;
    private const float EdgeFeather = 0.22f;
    private const float FlickerFreq = 8f;
    private const float FlickerMin = 0.85f;
    private const float FlickerRange = 0.15f;
    private const float GlowCoreRadius = 0.35f;
    private const float GlowOuterRadius = 0.85f;
    private const float GlowCoreBrighten = 1.3f;
    private const float GlowFalloffAlpha = 0.7f;
    private const float TankHeatGlowMinHeat = 5f;
    private const float TankHeatGlowBaseRadius = 2.5f;
    private const float TankHeatGlowScaleRadius = 2.5f;
    private const float TankHeatGlowR = 200f;
    private const float TankHeatGlowG = 60f;
    private const float TankHeatGlowB = 10f;

    public void Render(
        uint[] targetPixels,
        int targetWidth,
        int targetHeight,
        uint[] terrainPixels,
        uint[] compositePixels,
        int worldWidth,
        int worldHeight,
        int camPixelX,
        int camPixelY,
        int pixelScale,
        float time = 0f)
    {
        int cellMinX = Math.Max(0, camPixelX / pixelScale);
        int cellMinY = Math.Max(0, camPixelY / pixelScale);
        int cellMaxX = Math.Min(worldWidth - 1, (camPixelX + targetWidth - 1) / pixelScale);
        int cellMaxY = Math.Min(worldHeight - 1, (camPixelY + targetHeight - 1) / pixelScale);

        // Pass 1: Drop shadows (rendered first so entities draw on top)
        for (int wy = cellMinY; wy <= cellMaxY; wy++)
        {
            int worldRow = wy * worldWidth;
            for (int wx = cellMinX; wx <= cellMaxX; wx++)
            {
                int idx = worldRow + wx;
                if (compositePixels[idx] == terrainPixels[idx])
                    continue;

                int shadowCellX = wx + 1;
                int shadowCellY = wy + 1;
                if (shadowCellX >= worldWidth || shadowCellY >= worldHeight)
                    continue;
                int shadowIdx = shadowCellX + shadowCellY * worldWidth;
                if (compositePixels[shadowIdx] != terrainPixels[shadowIdx])
                    continue;

                int baseX = shadowCellX * pixelScale - camPixelX;
                int baseY = shadowCellY * pixelScale - camPixelY;
                int sx0 = Math.Max(0, baseX);
                int sx1 = Math.Min(targetWidth, baseX + pixelScale);
                int sy0 = Math.Max(0, baseY);
                int sy1 = Math.Min(targetHeight, baseY + pixelScale);

                for (int py = sy0; py < sy1; py++)
                {
                    int row = py * targetWidth;
                    for (int px = sx0; px < sx1; px++)
                        targetPixels[row + px] = RenderingPixels.Darken(targetPixels[row + px], ShadowDarken);
                }
            }
        }

        // Pass 2: Entity cells with edge anti-aliasing + projectile glow
        for (int wy = cellMinY; wy <= cellMaxY; wy++)
        {
            int worldRow = wy * worldWidth;
            for (int wx = cellMinX; wx <= cellMaxX; wx++)
            {
                int idx = worldRow + wx;
                uint terrainColor = terrainPixels[idx];
                uint objectColor = compositePixels[idx];
                if (objectColor == terrainColor)
                    continue;

                bool isIsolated = IsIsolatedEntity(
                    compositePixels, terrainPixels, wx, wy, worldWidth, worldHeight);

                int baseScreenX = wx * pixelScale - camPixelX;
                int baseScreenY = wy * pixelScale - camPixelY;

                bool neighborLeft = wx > 0 && compositePixels[idx - 1] != terrainPixels[idx - 1];
                bool neighborRight = wx < worldWidth - 1 && compositePixels[idx + 1] != terrainPixels[idx + 1];
                bool neighborUp = wy > 0 && compositePixels[idx - worldWidth] != terrainPixels[idx - worldWidth];
                bool neighborDown = wy < worldHeight - 1 && compositePixels[idx + worldWidth] != terrainPixels[idx + worldWidth];

                if (isIsolated)
                {
                    uint cellSeed = RenderingMath.Hash2((uint)wx, (uint)wy);
                    float phase = (cellSeed & 0xFFu) / 255f * 6.28f;
                    float flicker = FlickerMin + FlickerRange * MathF.Sin(time * FlickerFreq + phase);
                    RenderGlowEntity(targetPixels, targetWidth, targetHeight,
                        baseScreenX, baseScreenY, pixelScale, objectColor, flicker);
                }
                else
                {
                    RenderAAEntity(targetPixels, targetWidth, targetHeight,
                        baseScreenX, baseScreenY, pixelScale, objectColor,
                        neighborLeft, neighborRight, neighborUp, neighborDown);
                }
            }
        }
    }

    private static void RenderAAEntity(
        uint[] target, int tw, int th,
        int bx, int by, int scale, uint entityColor,
        bool nLeft, bool nRight, bool nUp, bool nDown)
    {
        float feather = EdgeFeather;

        for (int py = 0; py < scale; py++)
        {
            int sy = by + py;
            if (sy < 0 || sy >= th) continue;
            float fy = (py + 0.5f) / scale;
            int row = sy * tw;

            for (int px = 0; px < scale; px++)
            {
                int sx = bx + px;
                if (sx < 0 || sx >= tw) continue;
                float fx = (px + 0.5f) / scale;

                float alpha = 1f;
                if (!nLeft)   alpha = MathF.Min(alpha, Smoothstep(0f, feather, fx));
                if (!nRight)  alpha = MathF.Min(alpha, Smoothstep(0f, feather, 1f - fx));
                if (!nUp)     alpha = MathF.Min(alpha, Smoothstep(0f, feather, fy));
                if (!nDown)   alpha = MathF.Min(alpha, Smoothstep(0f, feather, 1f - fy));

                target[row + sx] = RenderingPixels.Blend(target[row + sx], entityColor, alpha);
            }
        }
    }

    private static void RenderGlowEntity(
        uint[] target, int tw, int th,
        int bx, int by, int scale, uint entityColor, float flicker = 1f)
    {
        float cx = bx + scale * 0.5f;
        float cy = by + scale * 0.5f;
        float coreR = scale * GlowCoreRadius;
        float glowR = scale * GlowOuterRadius * flicker;

        int minPx = Math.Max(0, bx - scale / 2);
        int maxPx = Math.Min(tw - 1, bx + scale + scale / 2);
        int minPy = Math.Max(0, by - scale / 2);
        int maxPy = Math.Min(th - 1, by + scale + scale / 2);

        for (int py = minPy; py <= maxPy; py++)
        {
            float dy = py + 0.5f - cy;
            int row = py * tw;
            for (int px = minPx; px <= maxPx; px++)
            {
                float dx = px + 0.5f - cx;
                float r = MathF.Sqrt(dx * dx + dy * dy);

                if (r <= coreR)
                {
                    target[row + px] = RenderingPixels.Brighten(entityColor, GlowCoreBrighten);
                }
                else if (r <= glowR)
                {
                    float t = (r - coreR) / (glowR - coreR);
                    float alpha = (1f - t) * (1f - t) * GlowFalloffAlpha;
                    target[row + px] = RenderingPixels.Blend(target[row + px], entityColor, alpha);
                }
            }
        }
    }

    private static bool IsIsolatedEntity(
        uint[] composite, uint[] terrain,
        int wx, int wy, int ww, int wh)
    {
        int idx = wx + wy * ww;
        if (wx > 0 && composite[idx - 1] != terrain[idx - 1]) return false;
        if (wx < ww - 1 && composite[idx + 1] != terrain[idx + 1]) return false;
        if (wy > 0 && composite[idx - ww] != terrain[idx - ww]) return false;
        if (wy < wh - 1 && composite[idx + ww] != terrain[idx + ww]) return false;
        return true;
    }

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        return RenderingMath.Smoothstep(edge0, edge1, x);
    }

    public static void RenderTankHeatGlow(
        uint[] targetPixels, int tw, int th,
        IReadOnlyList<Tank> tanks,
        int camPixelX, int camPixelY, int pixelScale)
    {
        for (int i = 0; i < tanks.Count; i++)
        {
            var tank = tanks[i];
            if (tank.IsDead || tank.Heat < TankHeatGlowMinHeat) continue;

            float t = tank.Heat / Tweaks.Tank.HeatMax;
            float intensity = t * t;
            int glowRadius = (int)(pixelScale * (TankHeatGlowBaseRadius + TankHeatGlowScaleRadius * t));

            float cx = (tank.Position.X + 0.5f) * pixelScale - camPixelX;
            float cy = (tank.Position.Y + 0.5f) * pixelScale - camPixelY;

            int x0 = Math.Max(0, (int)(cx - glowRadius));
            int y0 = Math.Max(0, (int)(cy - glowRadius));
            int x1 = Math.Min(tw - 1, (int)(cx + glowRadius));
            int y1 = Math.Min(th - 1, (int)(cy + glowRadius));
            float invR2 = 1f / (glowRadius * glowRadius);

            for (int py = y0; py <= y1; py++)
            {
                float dy = py - cy;
                float dy2 = dy * dy;
                int row = py * tw;
                for (int px = x0; px <= x1; px++)
                {
                    float dx = px - cx;
                    float dist2 = dx * dx + dy2;
                    float falloff = 1f - dist2 * invR2;
                    if (falloff <= 0f) continue;
                    falloff *= falloff;

                    float glow = intensity * falloff;
                    int addR = (int)(TankHeatGlowR * glow);
                    int addG = (int)(TankHeatGlowG * glow * t);
                    int addB = (int)(TankHeatGlowB * glow * t * t);

                    uint c = targetPixels[row + px];
                    int fr = Math.Min(255, (int)((c >> 16) & 0xFF) + addR);
                    int fg = Math.Min(255, (int)((c >> 8) & 0xFF) + addG);
                    int fb = Math.Min(255, (int)(c & 0xFF) + addB);
                    targetPixels[row + px] = 0xFF000000u | ((uint)fr << 16) | ((uint)fg << 8) | (uint)fb;
                }
            }
        }
    }
}
