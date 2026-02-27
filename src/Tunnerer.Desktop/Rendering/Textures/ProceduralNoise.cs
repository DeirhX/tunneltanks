namespace Tunnerer.Desktop.Rendering.Textures;

using Tunnerer.Core.Types;
using Tunnerer.Desktop.Rendering;

/// <summary>
/// Procedural noise utilities for generating tileable textures.
/// All functions produce seamlessly tileable output for a given texture size.
/// </summary>
public static class ProceduralNoise
{
    // ----------------------------------------------------------------
    //  Core hash — same as HiResTerrainRenderer but exposed here
    // ----------------------------------------------------------------

    public static uint Hash2(uint x, uint y)
    {
        return RenderingMath.Hash2(x, y);
    }

    public static float HashNorm(uint x, uint y) => (Hash2(x, y) & 0xFFFFu) / 65535f;

    // ----------------------------------------------------------------
    //  Value noise with smooth interpolation (tileable)
    // ----------------------------------------------------------------

    public static float ValueNoise(float x, float y, int period)
    {
        int ix = (int)MathF.Floor(x);
        int iy = (int)MathF.Floor(y);
        float fx = x - ix;
        float fy = y - iy;

        // Smooth hermite interpolation
        float sx = fx * fx * (3f - 2f * fx);
        float sy = fy * fy * (3f - 2f * fy);

        // Wrap coordinates for tiling
        int x0 = ((ix % period) + period) % period;
        int y0 = ((iy % period) + period) % period;
        int x1 = (x0 + 1) % period;
        int y1 = (y0 + 1) % period;

        float n00 = HashNorm((uint)x0, (uint)y0);
        float n10 = HashNorm((uint)x1, (uint)y0);
        float n01 = HashNorm((uint)x0, (uint)y1);
        float n11 = HashNorm((uint)x1, (uint)y1);

        float top = n00 + sx * (n10 - n00);
        float bot = n01 + sx * (n11 - n01);
        return top + sy * (bot - top);
    }

    // ----------------------------------------------------------------
    //  Fractal Brownian Motion (fBm) — multiple octaves, tileable
    // ----------------------------------------------------------------

    public static float Fbm(float x, float y, int basePeriod, int octaves,
        float lacunarity = 2f, float gain = 0.5f, uint seedOffset = 0)
    {
        float sum = 0f, amp = 1f, freq = 1f, maxAmp = 0f;
        int period = basePeriod;

        for (int i = 0; i < octaves; i++)
        {
            // Offset each octave so they don't correlate
            float ox = (seedOffset + (uint)i * 31u) * 17.3f;
            float oy = (seedOffset + (uint)i * 59u) * 13.7f;
            sum += amp * ValueNoise(x * freq + ox, y * freq + oy, period);
            maxAmp += amp;
            amp *= gain;
            freq *= lacunarity;
            period *= (int)lacunarity;
        }

        return sum / maxAmp;
    }

    // ----------------------------------------------------------------
    //  Worley (cellular) noise — tileable
    // ----------------------------------------------------------------

    public static float Worley(float x, float y, int period, uint seed = 0)
    {
        int ix = (int)MathF.Floor(x);
        int iy = (int)MathF.Floor(y);
        float minDist = float.MaxValue;

        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int cx = ix + dx;
            int cy = iy + dy;
            int wx = ((cx % period) + period) % period;
            int wy = ((cy % period) + period) % period;

            // Jittered point within the cell
            float jx = cx + HashNorm((uint)wx + seed, (uint)wy + seed * 7u);
            float jy = cy + HashNorm((uint)wx + seed * 13u, (uint)wy + seed * 19u);

            float ddx = x - jx;
            float ddy = y - jy;
            float dist = ddx * ddx + ddy * ddy;
            if (dist < minDist) minDist = dist;
        }

        return MathF.Sqrt(minDist);
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        float t = (value - fromMin) / (fromMax - fromMin);
        t = MathF.Max(0f, MathF.Min(1f, t));
        return toMin + t * (toMax - toMin);
    }

    public static Color LerpColor(Color a, Color b, float t)
    {
        t = MathF.Max(0f, MathF.Min(1f, t));
        return new Color(
            (byte)(a.R + (b.R - a.R) * t + 0.5f),
            (byte)(a.G + (b.G - a.G) * t + 0.5f),
            (byte)(a.B + (b.B - a.B) * t + 0.5f));
    }
}
