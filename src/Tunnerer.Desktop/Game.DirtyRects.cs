namespace Tunnerer.Desktop;

using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;

public partial class Game
{
    private Rect ComputeVisibleWorldRect()
    {
        int scale = DesktopScreenTweaks.PixelScale;
        int viewMinX = Math.Max(0, _camPixelX / scale);
        int viewMinY = Math.Max(0, _camPixelY / scale);
        int viewW = (_hiResSize.X + scale - 1) / scale;
        int viewH = (_hiResSize.Y + scale - 1) / scale;
        int viewMaxX = Math.Min(_terrainSize.X, viewMinX + viewW);
        int viewMaxY = Math.Min(_terrainSize.Y, viewMinY + viewH);
        return new Rect(viewMinX, viewMinY, viewMaxX - viewMinX, viewMaxY - viewMinY);
    }

    private Rect? ClampToViewport(Rect? dirty, in Rect viewport)
    {
        if (dirty is null) return null;
        return RectMath.Intersect(dirty.Value, viewport);
    }

    private Rect? ComputeCameraRevealRect(in Rect currentViewport)
    {
        if (_lastAuxViewport.Width == 0 || _lastAuxViewport.Height == 0)
            return currentViewport;

        if (currentViewport.Left == _lastAuxViewport.Left &&
            currentViewport.Top == _lastAuxViewport.Top &&
            currentViewport.Right == _lastAuxViewport.Right &&
            currentViewport.Bottom == _lastAuxViewport.Bottom)
            return null;

        // Compute the visible area minus what was visible last frame.
        // Use a conservative bounding rect of the newly-exposed strips.
        int newLeft = Math.Min(currentViewport.Left, _lastAuxViewport.Left);
        int newTop = Math.Min(currentViewport.Top, _lastAuxViewport.Top);
        int newRight = Math.Max(currentViewport.Right, _lastAuxViewport.Right);
        int newBottom = Math.Max(currentViewport.Bottom, _lastAuxViewport.Bottom);

        Rect? reveal = null;

        if (currentViewport.Left < _lastAuxViewport.Left)
            reveal = RectMath.Union(reveal, new Rect(currentViewport.Left, currentViewport.Top,
                _lastAuxViewport.Left - currentViewport.Left, currentViewport.Height));

        if (currentViewport.Right > _lastAuxViewport.Right)
            reveal = RectMath.Union(reveal, new Rect(_lastAuxViewport.Right, currentViewport.Top,
                currentViewport.Right - _lastAuxViewport.Right, currentViewport.Height));

        if (currentViewport.Top < _lastAuxViewport.Top)
            reveal = RectMath.Union(reveal, new Rect(currentViewport.Left, currentViewport.Top,
                currentViewport.Width, _lastAuxViewport.Top - currentViewport.Top));

        if (currentViewport.Bottom > _lastAuxViewport.Bottom)
            reveal = RectMath.Union(reveal, new Rect(currentViewport.Left, _lastAuxViewport.Bottom,
                currentViewport.Width, currentViewport.Bottom - _lastAuxViewport.Bottom));

        return reveal;
    }

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
