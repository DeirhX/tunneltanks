namespace TunnelTanks.Core.Collision;

using TunnelTanks.Core.Types;

public static class Raycaster
{
    public static IEnumerable<Position> CastRay(PositionF origin, VectorF direction, int maxSteps)
    {
        float x = origin.X, y = origin.Y;
        float dx = direction.X, dy = direction.Y;

        for (int i = 0; i < maxSteps; i++)
        {
            x += dx;
            y += dy;
            yield return new Position((int)x, (int)y);
        }
    }

    public static void BresenhamLine(Position from, Position to, Action<Position> visit)
    {
        int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            visit(new Position(x0, y0));
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public static bool BresenhamLineAny(Position from, Position to, Func<Position, bool> predicate)
    {
        int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            if (predicate(new Position(x0, y0))) return true;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
        return false;
    }
}
