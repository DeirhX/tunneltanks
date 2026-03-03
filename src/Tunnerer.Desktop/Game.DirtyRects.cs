namespace Tunnerer.Desktop;

using Tunnerer.Core.Types;

public partial class Game
{
    private static Rect? TryGetDirtyCellBounds(IReadOnlyList<Position> dirtyCells)
    {
        if (dirtyCells.Count == 0)
            return null;

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            var p = dirtyCells[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return RectMath.FromMinMaxInclusive(minX, minY, maxX, maxY);
    }

    private static Rect? MergeDirtyRects(Rect? a, Rect? b)
    {
        return RectMath.Union(a, b);
    }
}
