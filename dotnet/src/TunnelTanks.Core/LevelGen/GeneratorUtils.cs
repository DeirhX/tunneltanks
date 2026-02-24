namespace TunnelTanks.Core.LevelGen;

using System.Runtime.CompilerServices;
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

    public static int PointDistanceSquared(Position a, Position b) => Position.DistanceSquared(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInterior(int x, int y, int w, int h) =>
        x > 0 && x < w - 1 && y > 0 && y < h - 1;

    public static void DrawLine(TerrainGrid terrain, Position from, Position to, TerrainPixel value)
    {
        WalkBresenham(from, to, (x, y) =>
            terrain.SetPixelRaw(new Position(x, y), value));
    }

    public static void DrawThickLine(TerrainGrid terrain, Position from, Position to, TerrainPixel value, int radius)
    {
        int w = terrain.Width, h = terrain.Height;
        WalkBresenham(from, to, (cx, cy) =>
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (IsInterior(nx, ny, w, h))
                        terrain.SetPixelRaw(new Position(nx, ny), value);
                }
        });
    }

    public static void FillAll(TerrainGrid terrain, TerrainPixel value)
    {
        terrain.Fill(value);
    }

    public static void SetOutside(TerrainGrid terrain, TerrainPixel value)
    {
        int w = terrain.Width, h = terrain.Height;
        for (int x = 0; x < w; x++)
        {
            terrain.SetPixelRaw(new Position(x, 0), value);
            terrain.SetPixelRaw(new Position(x, h - 1), value);
        }
        for (int y = 1; y < h - 1; y++)
        {
            terrain.SetPixelRaw(new Position(0, y), value);
            terrain.SetPixelRaw(new Position(w - 1, y), value);
        }
    }

    private static void WalkBresenham(Position from, Position to, Action<int, int> visit)
    {
        int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            visit(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
}
