namespace Tunnerer.Desktop.Rendering;

public sealed class HiResEntityRenderer
{
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
                        targetPixels[row + px] = DarkenPixel(targetPixels[row + px], 0.82f);
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
                    uint cellSeed = (uint)(wx * 374761393 + wy * 668265263);
                    float phase = (cellSeed & 0xFFu) / 255f * 6.28f;
                    float flicker = 0.85f + 0.15f * MathF.Sin(time * 8f + phase);
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
        float feather = 0.22f;

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

                target[row + sx] = BlendPixel(target[row + sx], entityColor, alpha);
            }
        }
    }

    private static void RenderGlowEntity(
        uint[] target, int tw, int th,
        int bx, int by, int scale, uint entityColor, float flicker = 1f)
    {
        float cx = bx + scale * 0.5f;
        float cy = by + scale * 0.5f;
        float coreR = scale * 0.35f;
        float glowR = scale * 0.85f * flicker;

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
                    target[row + px] = BrightenPixel(entityColor, 1.3f);
                }
                else if (r <= glowR)
                {
                    float t = (r - coreR) / (glowR - coreR);
                    float alpha = (1f - t) * (1f - t) * 0.7f;
                    target[row + px] = BlendPixel(target[row + px], entityColor, alpha);
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

    private static uint BlendPixel(uint under, uint over, float alpha)
    {
        if (alpha >= 1f) return (under & 0xFF000000u) | (over & 0x00FFFFFFu);
        if (alpha <= 0f) return under;

        int ur = (int)((under >> 16) & 0xFF);
        int ug = (int)((under >> 8) & 0xFF);
        int ub = (int)(under & 0xFF);

        int or2 = (int)((over >> 16) & 0xFF);
        int og = (int)((over >> 8) & 0xFF);
        int ob = (int)(over & 0xFF);

        int r = ur + (int)((or2 - ur) * alpha + 0.5f);
        int g = ug + (int)((og - ug) * alpha + 0.5f);
        int b = ub + (int)((ob - ub) * alpha + 0.5f);

        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    private static uint DarkenPixel(uint color, float factor)
    {
        int r = (int)(((color >> 16) & 0xFF) * factor);
        int g = (int)(((color >> 8) & 0xFF) * factor);
        int b = (int)((color & 0xFF) * factor);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    private static uint BrightenPixel(uint color, float factor)
    {
        int r = Math.Min(255, (int)(((color >> 16) & 0xFF) * factor));
        int g = Math.Min(255, (int)(((color >> 8) & 0xFF) * factor));
        int b = Math.Min(255, (int)((color & 0xFF) * factor));
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = (x - edge0) / (edge1 - edge0);
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return t * t * (3f - 2f * t);
    }
}
