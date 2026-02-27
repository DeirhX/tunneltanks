namespace Tunnerer.Desktop.Rendering;

public static class RenderingMath
{
    public static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = (x - edge0) / (edge1 - edge0);
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return t * t * (3f - 2f * t);
    }

    public static uint Hash2(uint x, uint y)
    {
        uint h = x * 374761393u + y * 668265263u + 0x9E3779B9u;
        h ^= h >> 13;
        h *= 1274126177u;
        h ^= h >> 16;
        return h;
    }

    public static float Bilinear(float v00, float v10, float v01, float v11, float lx, float ly)
    {
        float w00 = (1f - lx) * (1f - ly);
        float w10 = lx * (1f - ly);
        float w01 = (1f - lx) * ly;
        float w11 = lx * ly;
        return v00 * w00 + v10 * w10 + v01 * w01 + v11 * w11;
    }
}
