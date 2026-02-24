namespace TunnelTanks.Core.Gui;

using TunnelTanks.Core.Types;

public static class FontRenderer
{
    private const int CharWidth = 4;
    private const int CharHeight = 5;
    private const int Spacing = 1;

    private static readonly Dictionary<char, byte[]> Glyphs = new()
    {
        ['0'] = new byte[] { 1,1,1,0, 1,0,1,0, 1,0,1,0, 1,0,1,0, 1,1,1,0 },
        ['1'] = new byte[] { 0,1,0,0, 1,1,0,0, 0,1,0,0, 0,1,0,0, 1,1,1,0 },
        ['2'] = new byte[] { 1,1,1,0, 0,0,1,0, 1,1,1,0, 1,0,0,0, 1,1,1,0 },
        ['3'] = new byte[] { 1,1,1,0, 0,0,1,0, 0,1,1,0, 0,0,1,0, 1,1,1,0 },
        ['4'] = new byte[] { 1,0,1,0, 1,0,1,0, 1,1,1,0, 0,0,1,0, 0,0,1,0 },
        ['5'] = new byte[] { 1,1,1,0, 1,0,0,0, 1,1,1,0, 0,0,1,0, 1,1,1,0 },
        ['6'] = new byte[] { 1,1,1,0, 1,0,0,0, 1,1,1,0, 1,0,1,0, 1,1,1,0 },
        ['7'] = new byte[] { 1,1,1,0, 0,0,1,0, 0,0,1,0, 0,1,0,0, 0,1,0,0 },
        ['8'] = new byte[] { 1,1,1,0, 1,0,1,0, 1,1,1,0, 1,0,1,0, 1,1,1,0 },
        ['9'] = new byte[] { 1,1,1,0, 1,0,1,0, 1,1,1,0, 0,0,1,0, 1,1,1,0 },
        ['H'] = new byte[] { 1,0,1,0, 1,0,1,0, 1,1,1,0, 1,0,1,0, 1,0,1,0 },
        ['E'] = new byte[] { 1,1,1,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,1,0 },
        ['L'] = new byte[] { 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,1,1,0 },
        ['D'] = new byte[] { 1,1,0,0, 1,0,1,0, 1,0,1,0, 1,0,1,0, 1,1,0,0 },
        ['M'] = new byte[] { 1,0,1,0, 1,1,1,0, 1,1,1,0, 1,0,1,0, 1,0,1,0 },
        [':'] = new byte[] { 0,0,0,0, 0,1,0,0, 0,0,0,0, 0,1,0,0, 0,0,0,0 },
        ['/'] = new byte[] { 0,0,1,0, 0,0,1,0, 0,1,0,0, 1,0,0,0, 1,0,0,0 },
        [' '] = new byte[] { 0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,0 },
    };

    public static void DrawString(uint[] surface, int surfaceWidth, int surfaceHeight,
        int x, int y, string text, Color color)
    {
        uint argb = color.ToArgb();
        int cx = x;
        foreach (char ch in text.ToUpper())
        {
            if (Glyphs.TryGetValue(ch, out var glyph))
            {
                for (int gy = 0; gy < CharHeight; gy++)
                    for (int gx = 0; gx < CharWidth; gx++)
                    {
                        if (glyph[gx + gy * CharWidth] == 0) continue;
                        int px = cx + gx, py = y + gy;
                        if (px >= 0 && py >= 0 && px < surfaceWidth && py < surfaceHeight)
                            surface[px + py * surfaceWidth] = argb;
                    }
            }
            cx += CharWidth + Spacing;
        }
    }

    public static int MeasureWidth(string text) => text.Length * (CharWidth + Spacing) - Spacing;
}
