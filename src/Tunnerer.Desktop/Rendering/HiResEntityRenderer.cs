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
                    // Leave glow spread to GPU bloom; CPU renders a brightened core only.
                    uint core = RenderingPixels.Brighten(objectColor, 1.15f * flicker);
                    RenderAAEntity(targetPixels, targetWidth, targetHeight,
                        baseScreenX, baseScreenY, pixelScale, core,
                        neighborLeft, neighborRight, neighborUp, neighborDown);
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

}
