namespace Tunnerer.Desktop.Rendering;

using Tunnerer.Core.Types;

/// <summary>
/// Tileable procedural textures for each terrain material.
/// Generated once at startup, then sampled per-pixel during rendering.
/// </summary>
public sealed class MaterialTextures
{
    public const int Size = 128;
    private const int Mask = Size - 1;
    public const float TexelsPerUnit = 10f;

    public readonly uint[] Dirt = new uint[Size * Size];
    public readonly uint[] DirtGrow = new uint[Size * Size];
    public readonly uint[] Rock = new uint[Size * Size];
    public readonly uint[] Concrete = new uint[Size * Size];
    public readonly uint[] Cave = new uint[Size * Size];
    public readonly uint[] Scorched = new uint[Size * Size];
    public readonly uint[] Energy = new uint[Size * Size];

    public MaterialTextures()
    {
        GenerateDirt();
        GenerateDirtGrow();
        GenerateRock();
        GenerateConcrete();
        GenerateCave();
        GenerateScorched();
        GenerateEnergy();
    }

    // ------------------------------------------------------------------
    //  Bilinear texture sampling (returns Color for blending pipeline)
    // ------------------------------------------------------------------

    public static Color Sample(uint[] tex, float worldXf, float worldYf)
    {
        float u = worldXf * TexelsPerUnit;
        float v = worldYf * TexelsPerUnit;

        int x0 = ((int)MathF.Floor(u)) & Mask;
        int y0 = ((int)MathF.Floor(v)) & Mask;
        int x1 = (x0 + 1) & Mask;
        int y1 = (y0 + 1) & Mask;
        float lx = u - MathF.Floor(u);
        float ly = v - MathF.Floor(v);

        uint c00 = tex[y0 * Size + x0];
        uint c10 = tex[y0 * Size + x1];
        uint c01 = tex[y1 * Size + x0];
        uint c11 = tex[y1 * Size + x1];

        return BilinearColor(c00, c10, c01, c11, lx, ly);
    }

    private static Color BilinearColor(uint c00, uint c10, uint c01, uint c11, float lx, float ly)
    {
        float r = Bilerp(
            (c00 >> 16) & 0xFF, (c10 >> 16) & 0xFF,
            (c01 >> 16) & 0xFF, (c11 >> 16) & 0xFF, lx, ly);
        float g = Bilerp(
            (c00 >> 8) & 0xFF, (c10 >> 8) & 0xFF,
            (c01 >> 8) & 0xFF, (c11 >> 8) & 0xFF, lx, ly);
        float b = Bilerp(
            c00 & 0xFF, c10 & 0xFF,
            c01 & 0xFF, c11 & 0xFF, lx, ly);

        return new Color(ClampByte(r), ClampByte(g), ClampByte(b));
    }

    private static float Bilerp(float v00, float v10, float v01, float v11, float lx, float ly)
        => (v00 * (1 - lx) + v10 * lx) * (1 - ly) + (v01 * (1 - lx) + v11 * lx) * ly;

    private static byte ClampByte(float v) =>
        (byte)(v < 0 ? 0 : v > 255 ? 255 : (int)(v + 0.5f));

    // ------------------------------------------------------------------
    //  Texture generation: Dirt
    // ------------------------------------------------------------------

    private void GenerateDirt()
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n0 = Fbm(x, y, 8, 3, 0);
                float n1 = Fbm(x, y, 16, 2, 100);
                float fine = HashTexel(x, y, 200);

                float r = 162 + n0 * 40f - n1 * 22f + fine * 12f;
                float g = 102 + n0 * 28f - n1 * 16f + fine * 8f;
                float b = 48 + n0 * 18f - n1 * 10f + fine * 6f;

                // Occasional darker splotches
                float splotch = Fbm(x, y, 4, 2, 300);
                if (splotch > 0.62f)
                {
                    float s = (splotch - 0.62f) / 0.38f;
                    r -= s * 28f;
                    g -= s * 20f;
                    b -= s * 12f;
                }

                Dirt[y * Size + x] = Pack(r, g, b);
            }
    }

    private void GenerateDirtGrow()
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n0 = Fbm(x, y, 8, 3, 0);
                float n1 = Fbm(x, y, 16, 2, 100);
                float fine = HashTexel(x, y, 200);

                float r = 130 + n0 * 30f - n1 * 15f + fine * 8f;
                float g = 120 + n0 * 25f + n1 * 10f + fine * 6f;
                float b = 58 + n0 * 16f - n1 * 8f + fine * 4f;

                DirtGrow[y * Size + x] = Pack(r, g, b);
            }
    }

    // ------------------------------------------------------------------
    //  Texture generation: Rock
    // ------------------------------------------------------------------

    private void GenerateRock()
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n0 = Fbm(x, y, 8, 3, 500);
                float fine = HashTexel(x, y, 600);

                // Horizontal strata with slight waviness
                float waveX = Fbm(x, y, 4, 2, 550) * 4f;
                float strataY = (y + waveX) * 0.15f;
                float strata = MathF.Abs(strataY - MathF.Floor(strataY) - 0.5f) * 2f;

                float r = 82 + n0 * 20f + strata * 14f - fine * 6f;
                float g = 78 + n0 * 18f + strata * 12f - fine * 5f;
                float b = 72 + n0 * 16f + strata * 10f - fine * 4f;

                Rock[y * Size + x] = Pack(r, g, b);
            }
    }

    // ------------------------------------------------------------------
    //  Texture generation: Concrete
    // ------------------------------------------------------------------

    private void GenerateConcrete()
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n0 = Fbm(x, y, 8, 3, 700);
                float fine = HashTexel(x, y, 800);

                float r = 110 + n0 * 18f + fine * 8f;
                float g = 110 + n0 * 18f + fine * 8f;
                float b = 118 + n0 * 16f + fine * 6f;

                // Grid joints
                float jx = MathF.Abs(((x + 0.5f) / Size * 5f) % 1f - 0.5f);
                float jy = MathF.Abs(((y + 0.5f) / Size * 3.5f) % 1f - 0.5f);
                float joint = MathF.Min(jx, jy);
                if (joint < 0.06f)
                {
                    float t = 1f - joint / 0.06f;
                    float darken = t * 30f;
                    r -= darken;
                    g -= darken;
                    b -= darken * 0.9f;
                }

                // Scattered aggregate specks
                float speck = HashTexel(x * 3 + 11, y * 3 + 17, 850);
                if (speck > 0.85f)
                {
                    float s = (speck - 0.85f) / 0.15f * 15f;
                    r += s;
                    g += s;
                    b += s * 0.8f;
                }

                Concrete[y * Size + x] = Pack(r, g, b);
            }
    }

    // ------------------------------------------------------------------
    //  Texture generation: Cave (empty space background)
    // ------------------------------------------------------------------

    private void GenerateCave()
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n0 = Fbm(x, y, 8, 3, 900);
                float fine = HashTexel(x, y, 950);

                float r = 16 + n0 * 10f + fine * 4f;
                float g = 15 + n0 * 9f + fine * 3f;
                float b = 19 + n0 * 11f + fine * 5f;

                Cave[y * Size + x] = Pack(r, g, b);
            }
    }

    // ------------------------------------------------------------------
    //  Texture generation: Scorched
    // ------------------------------------------------------------------

    private void GenerateScorched()
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n0 = Fbm(x, y, 8, 3, 1100);
                float fine = HashTexel(x, y, 1200);

                float r = 36 + n0 * 16f + fine * 6f;
                float g = 32 + n0 * 12f + fine * 5f;
                float b = 28 + n0 * 10f + fine * 4f;

                // Radial crack pattern
                float cx = (x % 32) - 16f;
                float cy = (y % 32) - 16f;
                float angle = MathF.Atan2(cy, cx);
                float rad = MathF.Sqrt(cx * cx + cy * cy);
                float crack = MathF.Abs(MathF.Sin(angle * 4f + n0 * 6f));
                if (crack < 0.12f && rad > 3f && rad < 14f)
                {
                    r -= 14f;
                    g -= 12f;
                    b -= 10f;
                }

                Scorched[y * Size + x] = Pack(r, g, b);
            }
    }

    // ------------------------------------------------------------------
    //  Texture generation: Energy
    // ------------------------------------------------------------------

    private void GenerateEnergy()
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n0 = Fbm(x, y, 8, 3, 1300);
                float fine = HashTexel(x, y, 1400);

                float r = 160 + n0 * 60f + fine * 20f;
                float g = 180 + n0 * 50f + fine * 15f;
                float b = 48 + n0 * 30f + fine * 10f;

                Energy[y * Size + x] = Pack(r, g, b);
            }
    }

    // ------------------------------------------------------------------
    //  Noise: Tileable FBM (Fractional Brownian Motion)
    // ------------------------------------------------------------------

    private static float Fbm(float x, float y, int baseGridSize, int octaves, uint seed)
    {
        float value = 0f, amp = 1f, maxAmp = 0f;
        int gridSize = baseGridSize;
        for (int o = 0; o < octaves; o++)
        {
            value += TileableNoise(x, y, gridSize, seed + (uint)(o * 7919)) * amp;
            maxAmp += amp;
            gridSize *= 2;
            amp *= 0.5f;
        }
        return value / maxAmp;
    }

    private static float TileableNoise(float x, float y, int gridSize, uint seed)
    {
        float cellSize = (float)Size / gridSize;
        float fx = x / cellSize;
        float fy = y / cellSize;

        int ix = (int)MathF.Floor(fx);
        int iy = (int)MathF.Floor(fy);
        float lx = fx - ix;
        float ly = fy - iy;

        int x0 = ((ix % gridSize) + gridSize) % gridSize;
        int y0 = ((iy % gridSize) + gridSize) % gridSize;
        int x1 = (x0 + 1) % gridSize;
        int y1 = (y0 + 1) % gridSize;

        float sx = lx * lx * (3 - 2 * lx);
        float sy = ly * ly * (3 - 2 * ly);

        float v00 = HashNorm(x0, y0, seed);
        float v10 = HashNorm(x1, y0, seed);
        float v01 = HashNorm(x0, y1, seed);
        float v11 = HashNorm(x1, y1, seed);

        return (v00 * (1 - sx) + v10 * sx) * (1 - sy)
             + (v01 * (1 - sx) + v11 * sx) * sy;
    }

    private static float HashTexel(int x, int y, uint seed)
    {
        uint h = (uint)(x & Mask) * 374761393u + (uint)(y & Mask) * 668265263u + seed;
        h ^= h >> 13;
        h *= 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFu) / 65535f;
    }

    private static float HashNorm(int x, int y, uint seed)
    {
        uint h = (uint)x * 374761393u + (uint)y * 668265263u + seed + 0x9E3779B9u;
        h ^= h >> 13;
        h *= 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFu) / 65535f;
    }

    private static uint Pack(float r, float g, float b)
    {
        byte rb = (byte)(r < 0 ? 0 : r > 255 ? 255 : (int)(r + 0.5f));
        byte gb = (byte)(g < 0 ? 0 : g > 255 ? 255 : (int)(g + 0.5f));
        byte bb = (byte)(b < 0 ? 0 : b > 255 ? 255 : (int)(b + 0.5f));
        return 0xFF000000u | ((uint)rb << 16) | ((uint)gb << 8) | bb;
    }
}
