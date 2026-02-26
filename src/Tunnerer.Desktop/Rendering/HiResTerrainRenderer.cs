namespace Tunnerer.Desktop.Rendering;

using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

public enum HiResRenderQuality
{
    Low = 0,
    Medium = 1,
    High = 2,
}

public sealed class HiResTerrainRenderer
{
    private const uint BackgroundColor = 0xFF161414;

    private readonly MaterialTextures _tex = new();

    private float[]? _blurField;
    private int _blurW;
    private int _blurH;

    private static readonly float[] Gauss5x5;
    static HiResTerrainRenderer()
    {
        const float sigma = 1.4f;
        Gauss5x5 = new float[25];
        for (int ky = -2; ky <= 2; ky++)
            for (int kx = -2; kx <= 2; kx++)
                Gauss5x5[(ky + 2) * 5 + (kx + 2)] =
                    MathF.Exp(-(kx * kx + ky * ky) / (2f * sigma * sigma));
    }

    // ------------------------------------------------------------------
    //  Blur-field management
    // ------------------------------------------------------------------

    public void RebuildBlurField(TerrainGrid terrain)
    {
        int w = terrain.Width, h = terrain.Height;
        if (_blurField == null || _blurW != w || _blurH != h)
        {
            _blurField = new float[w * h];
            _blurW = w;
            _blurH = h;
        }

        for (int cy = 0; cy < h; cy++)
            for (int cx = 0; cx < w; cx++)
                _blurField[cy * w + cx] = ComputeBlurCell(terrain, cx, cy, w, h);
    }

    public void UpdateBlurField(TerrainGrid terrain, IReadOnlyList<Position> dirtyCells)
    {
        if (_blurField == null)
        {
            RebuildBlurField(terrain);
            return;
        }

        int w = _blurW, h = _blurH;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            var p = dirtyCells[i];
            for (int dy = -2; dy <= 2; dy++)
            {
                int ny = p.Y + dy;
                if ((uint)ny >= (uint)h) continue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = p.X + dx;
                    if ((uint)nx >= (uint)w) continue;
                    _blurField[ny * w + nx] = ComputeBlurCell(terrain, nx, ny, w, h);
                }
            }
        }
    }

    private static float ComputeBlurCell(TerrainGrid terrain, int cx, int cy, int w, int h)
    {
        float sum = 0f, wSum = 0f;
        for (int ky = -2; ky <= 2; ky++)
        {
            int ny = cy + ky;
            if ((uint)ny >= (uint)h) continue;
            int rowOff = ny * w;
            for (int kx = -2; kx <= 2; kx++)
            {
                int nx = cx + kx;
                if ((uint)nx >= (uint)w) continue;
                float gw = Gauss5x5[(ky + 2) * 5 + (kx + 2)];
                sum += gw * (IsSolidTerrain(terrain.GetPixelRaw(rowOff + nx)) ? 1f : -1f);
                wSum += gw;
            }
        }
        return sum / wSum;
    }

    private float SampleBlur(int x, int y)
    {
        if ((uint)x >= (uint)_blurW || (uint)y >= (uint)_blurH)
            return 1f;
        return _blurField![y * _blurW + x];
    }

    // ------------------------------------------------------------------
    //  Public render entry points
    // ------------------------------------------------------------------

    public void Render(
        TerrainGrid terrain,
        uint[] targetPixels,
        int targetWidth,
        int targetHeight,
        HiResRenderQuality quality,
        int camPixelX,
        int camPixelY,
        int pixelScale)
    {
        RebuildBlurField(terrain);
        RenderRegion(terrain, targetPixels, targetWidth, targetHeight, quality,
            camPixelX, camPixelY, pixelScale, 0, 0, targetWidth - 1, targetHeight - 1);
    }

    public void RenderStrip(
        TerrainGrid terrain,
        uint[] targetPixels,
        int targetWidth,
        int targetHeight,
        HiResRenderQuality quality,
        int camPixelX,
        int camPixelY,
        int pixelScale,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        RenderRegion(terrain, targetPixels, targetWidth, targetHeight, quality,
            camPixelX, camPixelY, pixelScale, minX, minY, maxX, maxY);
    }

    public void RenderDirty(
        TerrainGrid terrain,
        uint[] targetPixels,
        int targetWidth,
        int targetHeight,
        HiResRenderQuality quality,
        int camPixelX,
        int camPixelY,
        int pixelScale,
        IReadOnlyList<Position> dirtyCells)
    {
        if (dirtyCells.Count == 0) return;

        UpdateBlurField(terrain, dirtyCells);

        const int pad = 3;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            var p = dirtyCells[i];
            int screenMinX = (p.X - pad) * pixelScale - camPixelX;
            int screenMaxX = (p.X + pad + 1) * pixelScale - 1 - camPixelX;
            int screenMinY = (p.Y - pad) * pixelScale - camPixelY;
            int screenMaxY = (p.Y + pad + 1) * pixelScale - 1 - camPixelY;

            screenMinX = Clamp(screenMinX, 0, targetWidth - 1);
            screenMaxX = Clamp(screenMaxX, 0, targetWidth - 1);
            screenMinY = Clamp(screenMinY, 0, targetHeight - 1);
            screenMaxY = Clamp(screenMaxY, 0, targetHeight - 1);

            if (screenMinX > screenMaxX || screenMinY > screenMaxY)
                continue;

            RenderRegion(terrain, targetPixels, targetWidth, targetHeight, quality,
                camPixelX, camPixelY, pixelScale, screenMinX, screenMinY, screenMaxX, screenMaxY);
        }
    }

    // ------------------------------------------------------------------
    //  Core render loop: texture-blended SDF rendering
    // ------------------------------------------------------------------

    private void RenderRegion(
        TerrainGrid terrain,
        uint[] targetPixels,
        int targetWidth,
        int targetHeight,
        HiResRenderQuality quality,
        int camPixelX,
        int camPixelY,
        int pixelScale,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        int w = terrain.Width;
        int h = terrain.Height;
        float invScale = 1f / pixelScale;

        int prevCellX = int.MinValue, prevCellY = int.MinValue;
        float aoValue = 0f;

        for (int y = minY; y <= maxY; y++)
        {
            float worldYf = (camPixelY + y + 0.5f) * invScale;
            int worldY = (int)worldYf;

            if (worldY < -1 || worldY > h)
            {
                int row = y * targetWidth;
                for (int x = minX; x <= maxX; x++)
                    targetPixels[row + x] = BackgroundColor;
                continue;
            }

            int writeIndex = minX + y * targetWidth;

            for (int x = minX; x <= maxX; x++, writeIndex++)
            {
                float worldXf = (camPixelX + x + 0.5f) * invScale;
                int worldX = (int)worldXf;

                if (worldX < -1 || worldX > w)
                {
                    targetPixels[writeIndex] = BackgroundColor;
                    continue;
                }

                float fracX = worldXf - worldX;
                float fracY = worldYf - worldY;

                // Bilinear interpolation of the Gaussian-blurred SDF
                float sx = fracX - 0.5f;
                float sy = fracY - 0.5f;
                int ox = sx >= 0f ? 0 : -1;
                int oy = sy >= 0f ? 0 : -1;
                float lx = sx >= 0f ? sx : sx + 1f;
                float ly = sy >= 0f ? sy : sy + 1f;

                int cx0 = worldX + ox, cx1 = cx0 + 1;
                int cy0 = worldY + oy, cy1 = cy0 + 1;

                float b00 = SampleBlur(cx0, cy0);
                float b10 = SampleBlur(cx1, cy0);
                float b01 = SampleBlur(cx0, cy1);
                float b11 = SampleBlur(cx1, cy1);
                float dist = b00 * (1f - lx) * (1f - ly) + b10 * lx * (1f - ly) +
                             b01 * (1f - lx) * ly + b11 * lx * ly;

                // Raw terrain for material type determination
                var centerPixel = SafeGet(terrain, worldX, worldY, w, h);

                // Ambient occlusion (cached per cell)
                if (worldX != prevCellX || worldY != prevCellY)
                {
                    prevCellX = worldX;
                    prevCellY = worldY;
                    aoValue = ComputeAO(terrain, worldX, worldY, w, h);
                }

                // SDF-based blend between solid texture and cave texture
                float edgeHalf = quality == HiResRenderQuality.Low ? 0.18f : 0.28f;
                float alpha;
                if (dist > edgeHalf)
                    alpha = 1f;
                else if (dist < -edgeHalf)
                    alpha = 0f;
                else
                    alpha = Smoothstep(-edgeHalf, edgeHalf, dist);

                Color blended;
                if (alpha >= 1f)
                {
                    blended = SampleSolidTexture(centerPixel, worldX, worldY, worldXf, worldYf);
                }
                else if (alpha <= 0f)
                {
                    blended = MaterialTextures.Sample(_tex.Cave, worldXf, worldYf);
                }
                else
                {
                    Color solidCol;
                    if (IsSolidTerrain(centerPixel))
                    {
                        solidCol = SampleSolidTexture(centerPixel, worldX, worldY, worldXf, worldYf);
                    }
                    else
                    {
                        var nearest = FindNearestSolid(terrain, cx0, cy0, cx1, cy1, w, h);
                        solidCol = SampleSolidTexture(nearest, worldX, worldY, worldXf, worldYf);
                    }
                    Color caveCol = MaterialTextures.Sample(_tex.Cave, worldXf, worldYf);
                    blended = LerpColor(caveCol, solidCol, alpha);
                }

                // Brightness modifiers
                float brightness = 1f;

                // Subtle per-cell variation to break up texture tiling
                brightness += CellVariation(worldX, worldY) * 0.08f;

                // Wall outline along boundary
                float absDist = MathF.Abs(dist);
                float outlineThick = quality == HiResRenderQuality.Low ? 0.10f : 0.16f;
                if (absDist < outlineThick)
                {
                    float t = 1f - absDist / outlineThick;
                    brightness -= t * t * 0.50f;
                }

                // Inner tunnel shadow
                if (dist < 0f && dist > -0.45f)
                {
                    float t = 1f + dist / 0.45f;
                    brightness -= t * t * 0.35f;
                }

                // Ambient occlusion on empty cells
                if (!IsSolidTerrain(centerPixel))
                    brightness -= aoValue * 0.45f;

                targetPixels[writeIndex] = ApplyBrightness(blended, brightness);
            }
        }
    }

    // ------------------------------------------------------------------
    //  Texture sampling for materials
    // ------------------------------------------------------------------

    private Color SampleSolidTexture(TerrainPixel pixel, int worldX, int worldY, float worldXf, float worldYf)
    {
        uint[] tex = GetTextureForPixel(pixel);
        if (tex == _tex.Cave)
        {
            // Base pixels: tint with the pixel's own color
            Color baseColor = Pixel.GetColor(pixel);
            Color texColor = MaterialTextures.Sample(_tex.Dirt, worldXf, worldYf);
            return LerpColor(baseColor, texColor, 0.25f);
        }
        return MaterialTextures.Sample(tex, worldXf, worldYf);
    }

    private uint[] GetTextureForPixel(TerrainPixel pixel)
    {
        if (pixel == TerrainPixel.DirtGrow) return _tex.DirtGrow;
        if (Pixel.IsDirt(pixel)) return _tex.Dirt;
        if (Pixel.IsRock(pixel)) return _tex.Rock;
        if (Pixel.IsConcrete(pixel)) return _tex.Concrete;
        if (Pixel.IsScorched(pixel)) return _tex.Scorched;
        if (Pixel.IsEnergy(pixel)) return _tex.Energy;
        if (Pixel.IsBase(pixel)) return _tex.Cave; // sentinel: handled specially
        return _tex.Dirt;
    }

    private static TerrainPixel FindNearestSolid(
        TerrainGrid terrain, int cx0, int cy0, int cx1, int cy1, int w, int h)
    {
        var p = SafeGet(terrain, cx0, cy0, w, h);
        if (IsSolidTerrain(p)) return p;
        p = SafeGet(terrain, cx1, cy0, w, h);
        if (IsSolidTerrain(p)) return p;
        p = SafeGet(terrain, cx0, cy1, w, h);
        if (IsSolidTerrain(p)) return p;
        p = SafeGet(terrain, cx1, cy1, w, h);
        if (IsSolidTerrain(p)) return p;
        return TerrainPixel.DirtHigh;
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static TerrainPixel SafeGet(TerrainGrid terrain, int x, int y, int w, int h)
    {
        if ((uint)x >= (uint)w || (uint)y >= (uint)h)
            return TerrainPixel.Rock;
        return terrain.GetPixelRaw(x + y * w);
    }

    private static float Smoothstep(float lo, float hi, float x)
    {
        float t = (x - lo) / (hi - lo);
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return t * t * (3f - 2f * t);
    }

    private static float ComputeAO(TerrainGrid terrain, int cx, int cy, int w, int h)
    {
        int solidCount = 0, sampleCount = 0;
        for (int dy = -2; dy <= 2; dy++)
        {
            int ny = cy + dy;
            if ((uint)ny >= (uint)h) continue;
            int rowOff = ny * w;
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = cx + dx;
                if ((uint)nx >= (uint)w) continue;
                sampleCount++;
                if (IsSolidTerrain(terrain.GetPixelRaw(rowOff + nx)))
                    solidCount++;
            }
        }
        return sampleCount > 0 ? (float)solidCount / sampleCount : 0f;
    }

    private static float CellVariation(int x, int y)
    {
        uint h = (uint)x * 374761393u + (uint)y * 668265263u + 0x9E3779B9u;
        h ^= h >> 13;
        h *= 1274126177u;
        h ^= h >> 16;
        return ((h & 0xFFFFu) / 65535f) - 0.5f;
    }

    private static uint ApplyBrightness(Color color, float brightness)
    {
        brightness = MathF.Max(0.2f, MathF.Min(1.8f, brightness));
        byte r = ScaleByte(color.R, brightness);
        byte g = ScaleByte(color.G, brightness);
        byte b = ScaleByte(color.B, brightness);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static byte ScaleByte(byte value, float scale)
    {
        int v = (int)(value * scale + 0.5f);
        return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        return value > max ? max : value;
    }

    private static bool IsSolidTerrain(TerrainPixel p)
    {
        if (p == TerrainPixel.Blank) return false;
        if (Pixel.IsScorched(p) || Pixel.IsEnergy(p)) return false;
        return true;
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = MathF.Max(0f, MathF.Min(1f, t));
        return new Color(
            LerpByte(a.R, b.R, t),
            LerpByte(a.G, b.G, t),
            LerpByte(a.B, b.B, t));
    }

    private static byte LerpByte(byte a, byte b, float t)
    {
        int v = (int)(a + (b - a) * t + 0.5f);
        return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
    }
}
