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
}
