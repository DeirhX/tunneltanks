namespace Tunnerer.Desktop;

public partial class Game
{
    private void ApplyThermalRegionDebugOverlay(uint[] pixels)
    {
        if (!_commandController.ShowThermalRegionDebug)
            return;
        if (!_world.Terrain.TryGetThermalTileInfo(out int tileSize, out int tileCountX, out int tileCountY))
            return;

        int width = _terrainSize.X;
        int height = _terrainSize.Y;
        for (int ty = 0; ty < tileCountY; ty++)
        {
            int minY = ty * tileSize;
            int maxY = Math.Min(height - 1, minY + tileSize - 1);
            for (int tx = 0; tx < tileCountX; tx++)
            {
                int minX = tx * tileSize;
                int maxX = Math.Min(width - 1, minX + tileSize - 1);
                bool active = _world.Terrain.IsThermalTileActive(tx, ty);

                byte tintR = active ? (byte)40 : (byte)35;
                byte tintG = active ? (byte)220 : (byte)110;
                byte tintB = active ? (byte)75 : (byte)220;
                float fillStrength = active ? 0.18f : 0.08f;
                float edgeStrength = active ? 0.60f : 0.42f;

                for (int y = minY; y <= maxY; y++)
                {
                    int row = y * width;
                    for (int x = minX; x <= maxX; x++)
                    {
                        bool border = x == minX || x == maxX || y == minY || y == maxY;
                        float strength = border ? edgeStrength : fillStrength;
                        int idx = row + x;
                        pixels[idx] = BlendTint(pixels[idx], tintR, tintG, tintB, strength);
                    }
                }
            }
        }
    }

    private static uint BlendTint(uint argb, byte tintR, byte tintG, byte tintB, float strength)
    {
        strength = Math.Clamp(strength, 0.0f, 1.0f);
        byte a = (byte)(argb >> 24);
        byte r = (byte)(argb >> 16);
        byte g = (byte)(argb >> 8);
        byte b = (byte)argb;

        int nr = (int)MathF.Round(r + (tintR - r) * strength);
        int ng = (int)MathF.Round(g + (tintG - g) * strength);
        int nb = (int)MathF.Round(b + (tintB - b) * strength);
        return ((uint)a << 24) | ((uint)nr << 16) | ((uint)ng << 8) | (uint)nb;
    }
}
