namespace TunnelTanks.Core.Rendering;

using TunnelTanks.Core.Types;

public static class TankSprites
{
    public const int SpriteWidth = 7;
    public const int SpriteHeight = 7;
    public const int DirectionCount = 9;

    public static readonly byte[][] Sprites = new byte[9][]
    {
        new byte[] {
            0,0,0,2,0,0,0,
            0,0,0,1,2,0,0,
            0,0,1,1,1,2,0,
            2,1,1,3,1,1,2,
            0,2,1,1,1,0,0,
            0,0,2,1,0,0,0,
            0,0,0,2,0,0,0},
        new byte[] {
            0,0,0,0,0,0,0,
            0,2,0,0,0,2,0,
            0,2,1,1,1,2,0,
            0,2,1,3,1,2,0,
            0,2,1,1,1,2,0,
            0,2,1,1,1,2,0,
            0,2,0,0,0,2,0},
        new byte[] {
            0,0,0,2,0,0,0,
            0,0,2,1,0,0,0,
            0,2,1,1,1,0,0,
            2,1,1,3,1,1,2,
            0,0,1,1,1,2,0,
            0,0,0,1,2,0,0,
            0,0,0,2,0,0,0},
        new byte[] {
            0,0,0,0,0,0,0,
            0,2,2,2,2,2,2,
            0,0,1,1,1,1,0,
            0,0,1,3,1,1,0,
            0,0,1,1,1,1,0,
            0,2,2,2,2,2,2,
            0,0,0,0,0,0,0},
        new byte[] { // unused direction slot
            0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,
            1,0,2,2,2,3,0,
            1,0,2,0,2,3,0,
            1,1,2,2,2,3,3,
            0,0,0,0,0,0,0,
            0,0,0,0,0,0,0},
        new byte[] {
            0,0,0,0,0,0,0,
            2,2,2,2,2,2,0,
            0,1,1,1,1,0,0,
            0,1,1,3,1,0,0,
            0,1,1,1,1,0,0,
            2,2,2,2,2,2,0,
            0,0,0,0,0,0,0},
        new byte[] {
            0,0,0,2,0,0,0,
            0,0,2,1,0,0,0,
            0,2,1,1,1,0,0,
            2,1,1,3,1,1,2,
            0,0,1,1,1,2,0,
            0,0,0,1,2,0,0,
            0,0,0,2,0,0,0},
        new byte[] {
            0,2,0,0,0,2,0,
            0,2,1,1,1,2,0,
            0,2,1,1,1,2,0,
            0,2,1,3,1,2,0,
            0,2,1,1,1,2,0,
            0,2,0,0,0,2,0,
            0,0,0,0,0,0,0},
        new byte[] {
            0,0,0,2,0,0,0,
            0,0,0,1,2,0,0,
            0,0,1,1,1,2,0,
            2,1,1,3,1,1,2,
            0,2,1,1,1,0,0,
            0,0,2,1,0,0,0,
            0,0,0,2,0,0,0},
    };

    public static readonly Color[][] TankColors = new Color[][]
    {
        // Blue tank
        new[] { new Color(0x2c, 0x2c, 0xff), new Color(0x00, 0x00, 0xb6), new Color(0xf3, 0xeb, 0x1c) },
        // Green tank
        new[] { new Color(0x00, 0xff, 0x00), new Color(0x00, 0xaa, 0x00), new Color(0xf3, 0xeb, 0x1c) },
        // Red tank
        new[] { new Color(0xff, 0x00, 0x00), new Color(0xaa, 0x00, 0x00), new Color(0xf3, 0xeb, 0x1c) },
        // Pink tank
        new[] { new Color(0xff, 0x99, 0x99), new Color(0xaa, 0x44, 0x44), new Color(0xf3, 0xeb, 0x1c) },
        // Purple tank
        new[] { new Color(0xff, 0x00, 0xff), new Color(0xaa, 0x00, 0xaa), new Color(0xf3, 0xeb, 0x1c) },
        // White tank
        new[] { new Color(0xee, 0xee, 0xee), new Color(0x99, 0x99, 0x99), new Color(0xf3, 0xeb, 0x1c) },
        // Aqua tank
        new[] { new Color(0x00, 0xff, 0xff), new Color(0x00, 0xaa, 0xaa), new Color(0xf3, 0xeb, 0x1c) },
        // Gray tank
        new[] { new Color(0x66, 0x66, 0x66), new Color(0x33, 0x33, 0x33), new Color(0xf3, 0xeb, 0x1c) },
    };

    public static Color GetPixelColor(int spriteValue, int tankColor)
    {
        if (spriteValue == 0) return Color.Transparent;
        var palette = tankColor < TankColors.Length ? TankColors[tankColor] : TankColors[0];
        return palette[Math.Min(spriteValue - 1, palette.Length - 1)];
    }
}
