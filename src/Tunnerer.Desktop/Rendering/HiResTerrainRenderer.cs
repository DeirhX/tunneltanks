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
        if (dirtyCells.Count == 0)
            return;

        const int pad = 2;
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

    private static void RenderRegion(
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
        int worldWidth = terrain.Width;
        int worldHeight = terrain.Height;
        float invScale = 1f / pixelScale;

        for (int y = minY; y <= maxY; y++)
        {
            float worldYf = (camPixelY + y + 0.5f) * invScale;
            int worldY = (int)worldYf;

            if (worldY < 0 || worldY >= worldHeight)
            {
                int row = y * targetWidth;
                for (int x = minX; x <= maxX; x++)
                    targetPixels[row + x] = BackgroundColor;
                continue;
            }

            float fracY = worldYf - worldY;
            int worldRow = worldY * worldWidth;
            int writeIndex = minX + y * targetWidth;

            for (int x = minX; x <= maxX; x++, writeIndex++)
            {
                float worldXf = (camPixelX + x + 0.5f) * invScale;
                int worldX = (int)worldXf;

                if (worldX < 0 || worldX >= worldWidth)
                {
                    targetPixels[writeIndex] = BackgroundColor;
                    continue;
                }

                float fracX = worldXf - worldX;

                var center = terrain.GetPixelRaw(worldRow + worldX);
                bool centerSolid = IsSolidTerrain(center);
                Color baseColor = MaterialColor(center, worldX, worldY, worldXf, worldYf, quality);

                float brightness = 1f;
                brightness += CellVariation(worldX, worldY, quality);
                brightness += LocalRelief(worldXf, worldYf, quality);
                brightness += ContourLighting(terrain, worldX, worldY, fracX, fracY, centerSolid, quality);

                targetPixels[writeIndex] = ApplyBrightness(baseColor, brightness);
            }
        }
    }

    private static Color MaterialColor(
        TerrainPixel pixel,
        int worldX,
        int worldY,
        float worldXf,
        float worldYf,
        HiResRenderQuality quality)
    {
        Color baseColor = Pixel.GetColor(pixel);

        uint coarseHash = Hash2((uint)(worldX >> 1), (uint)(worldY >> 1));
        uint detailHash = Hash2((uint)(worldX * 3 + ((int)(worldXf * 8f) & 7)), (uint)(worldY * 3 + ((int)(worldYf * 8f) & 7)));
        float macro = (coarseHash & 1023u) / 1023f;
        float micro = quality == HiResRenderQuality.Low ? 0f : (detailHash & 1023u) / 1023f;

        if (pixel == TerrainPixel.Blank)
            return Lerp(new Color(18, 18, 20), new Color(28, 28, 30), macro * 0.30f);

        if (Pixel.IsScorched(pixel))
            return Lerp(new Color(32, 30, 30), new Color(54, 46, 42), macro * 0.35f);

        if (Pixel.IsDirt(pixel) || pixel == TerrainPixel.DirtGrow)
        {
            Color warmA = new(182, 117, 58);
            Color warmB = new(153, 91, 40);
            Color dirt = Lerp(warmA, warmB, macro * 0.65f);
            dirt = Lerp(dirt, new Color(130, 79, 36), micro * 0.30f);
            if (pixel == TerrainPixel.DirtGrow)
                dirt = Lerp(dirt, new Color(94, 126, 74), 0.22f);
            return dirt;
        }

        if (Pixel.IsConcrete(pixel))
        {
            Color cA = new(124, 124, 132);
            Color cB = new(92, 92, 104);
            Color concrete = Lerp(cA, cB, macro * 0.7f);
            return Lerp(concrete, new Color(145, 145, 156), micro * 0.20f);
        }

        if (Pixel.IsRock(pixel))
        {
            Color rA = new(96, 90, 84);
            Color rB = new(72, 68, 64);
            Color rock = Lerp(rA, rB, macro * 0.75f);
            return Lerp(rock, new Color(122, 111, 102), micro * 0.12f);
        }

        if (Pixel.IsBase(pixel))
            return Lerp(baseColor, new Color(95, 95, 102), macro * 0.25f);

        if (Pixel.IsEnergy(pixel))
        {
            float pulse = ((Hash2((uint)(worldX * 5 + 19), (uint)(worldY * 7 + 31)) >> 8) & 255u) / 255f;
            return Lerp(new Color(150, 168, 48), new Color(240, 240, 96), pulse * 0.5f);
        }

        return baseColor;
    }

    private static float ContourLighting(
        TerrainGrid terrain,
        int x,
        int y,
        float fracX,
        float fracY,
        bool centerSolid,
        HiResRenderQuality quality)
    {
        int w = terrain.Width;
        int h = terrain.Height;
        float nearEdge = quality == HiResRenderQuality.High ? 0.32f : 0.24f;
        float rimGain = quality == HiResRenderQuality.Low ? 0.10f : 0.18f;
        float caveShadowGain = quality == HiResRenderQuality.Low ? 0.16f : 0.26f;

        float lighting = 0f;

        if (x > 0)
            lighting += BorderLight(centerSolid, IsSolidTerrain(terrain.GetPixelRaw(x - 1 + y * w)), 1f - fracX, nearEdge, rimGain, caveShadowGain);
        if (x < w - 1)
            lighting += BorderLight(centerSolid, IsSolidTerrain(terrain.GetPixelRaw(x + 1 + y * w)), fracX, nearEdge, rimGain, caveShadowGain);
        if (y > 0)
            lighting += BorderLight(centerSolid, IsSolidTerrain(terrain.GetPixelRaw(x + (y - 1) * w)), 1f - fracY, nearEdge, rimGain, caveShadowGain);
        if (y < h - 1)
            lighting += BorderLight(centerSolid, IsSolidTerrain(terrain.GetPixelRaw(x + (y + 1) * w)), fracY, nearEdge, rimGain, caveShadowGain);

        return lighting;
    }

    private static float BorderLight(
        bool centerSolid,
        bool neighborSolid,
        float edgeDistance,
        float edgeRange,
        float rimGain,
        float caveShadowGain)
    {
        if (centerSolid == neighborSolid)
            return 0f;

        float edge = 1f - MathF.Min(1f, edgeDistance / edgeRange);
        edge *= edge;

        if (centerSolid && !neighborSolid)
            return edge * rimGain;

        return -edge * caveShadowGain;
    }

    private static float CellVariation(int x, int y, HiResRenderQuality quality)
    {
        uint h = Hash2((uint)x, (uint)y);
        float n = ((h & 0xFFFFu) / 65535f) - 0.5f;
        return quality switch
        {
            HiResRenderQuality.Low => n * 0.10f,
            HiResRenderQuality.Medium => n * 0.14f,
            _ => n * 0.18f,
        };
    }

    private static float LocalRelief(float worldXf, float worldYf, HiResRenderQuality quality)
    {
        float k = quality switch
        {
            HiResRenderQuality.Low => 0.008f,
            HiResRenderQuality.Medium => 0.014f,
            _ => 0.020f,
        };

        int nx = (int)(worldXf * 2.0f);
        int ny = (int)(worldYf * 2.0f);
        float n = ((Hash2((uint)nx, (uint)ny) & 1023u) / 1023f) - 0.5f;
        return n * k;
    }

    private static uint ApplyBrightness(Color color, float brightness)
    {
        brightness = MathF.Max(0.2f, MathF.Min(1.8f, brightness));
        byte r = ScaleByte(color.R, brightness);
        byte g = ScaleByte(color.G, brightness);
        byte b = ScaleByte(color.B, brightness);
        return ((uint)0xFF << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static byte ScaleByte(byte value, float scale)
    {
        int v = (int)(value * scale + 0.5f);
        if (v < 0) return 0;
        if (v > 255) return 255;
        return (byte)v;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        return value > max ? max : value;
    }

    private static uint Hash2(uint x, uint y)
    {
        uint h = x * 374761393u + y * 668265263u + 0x9E3779B9u;
        h ^= h >> 13;
        h *= 1274126177u;
        h ^= h >> 16;
        return h;
    }

    private static bool IsSolidTerrain(TerrainPixel p)
    {
        if (p == TerrainPixel.Blank)
            return false;
        if (Pixel.IsScorched(p) || Pixel.IsEnergy(p))
            return false;
        return true;
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        t = MathF.Max(0f, MathF.Min(1f, t));
        byte r = LerpByte(a.R, b.R, t);
        byte g = LerpByte(a.G, b.G, t);
        byte bl = LerpByte(a.B, b.B, t);
        return new Color(r, g, bl);
    }

    private static byte LerpByte(byte a, byte b, float t)
    {
        int v = (int)(a + (b - a) * t + 0.5f);
        if (v < 0) return 0;
        if (v > 255) return 255;
        return (byte)v;
    }

}
