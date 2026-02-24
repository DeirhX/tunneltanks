namespace TunnelTanks.Core.LevelGen;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Terrain;
using TerrainGrid = TunnelTanks.Core.Terrain.Terrain;

public static class GeneratorUtils
{
    public static Position GenerateInside(Size size, int border, Random rng)
    {
        return new Position(
            rng.Next(border, size.X - border),
            rng.Next(border, size.Y - border));
    }

    public static int PointDistanceSquared(Position a, Position b)
    {
        int dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    public static void DrawLine(TerrainGrid terrain, Position from, Position to, TerrainPixel value)
    {
        int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            terrain.SetPixelRaw(new Position(x0, y0), value);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public static void FillAll(TerrainGrid terrain, TerrainPixel value)
    {
        for (int i = 0; i < terrain.Size.Area; i++)
            terrain[i] = value;
    }

    public static void SetOutside(TerrainGrid terrain, TerrainPixel value)
    {
        int w = terrain.Width, h = terrain.Height;
        for (int x = 0; x < w; x++) { terrain.SetPixelRaw(new Position(x, 0), value); terrain.SetPixelRaw(new Position(x, h - 1), value); }
        for (int y = 1; y < h - 1; y++) { terrain.SetPixelRaw(new Position(0, y), value); terrain.SetPixelRaw(new Position(w - 1, y), value); }
    }
}
