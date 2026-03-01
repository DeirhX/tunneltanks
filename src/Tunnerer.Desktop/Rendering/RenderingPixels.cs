namespace Tunnerer.Desktop.Rendering;

public static class RenderingPixels
{
    /// <summary>Alpha byte written into entity pixels so GPU shaders can distinguish them from terrain.
    /// 0xFE → 254/255 ≈ 0.996 in normalized float; shaders test <c>step(0.999, alpha)</c>.</summary>
    public const uint EntityAlpha = 0xFE000000u;

    public static uint PackRgb(float r, float g, float b)
    {
        byte rb = (byte)(r < 0 ? 0 : r > 255 ? 255 : (int)(r + 0.5f));
        byte gb = (byte)(g < 0 ? 0 : g > 255 ? 255 : (int)(g + 0.5f));
        byte bb = (byte)(b < 0 ? 0 : b > 255 ? 255 : (int)(b + 0.5f));
        return 0xFF000000u | ((uint)rb << 16) | ((uint)gb << 8) | bb;
    }

    public static uint MarkEntity(uint color) => (color & 0x00FFFFFFu) | EntityAlpha;

    public static uint Blend(uint under, uint over, float alpha)
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

    public static uint Darken(uint color, float factor)
    {
        int r = (int)(((color >> 16) & 0xFF) * factor);
        int g = (int)(((color >> 8) & 0xFF) * factor);
        int b = (int)((color & 0xFF) * factor);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    public static uint Brighten(uint color, float factor)
    {
        int r = Math.Min(255, (int)(((color >> 16) & 0xFF) * factor));
        int g = Math.Min(255, (int)(((color >> 8) & 0xFF) * factor));
        int b = Math.Min(255, (int)((color & 0xFF) * factor));
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    public static uint Additive(uint color, int addR, int addG, int addB)
    {
        int r = Math.Min(255, (int)((color >> 16) & 0xFF) + addR);
        int g = Math.Min(255, (int)((color >> 8) & 0xFF) + addG);
        int b = Math.Min(255, (int)(color & 0xFF) + addB);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }
}
