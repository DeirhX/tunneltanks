namespace Tunnerer.Desktop.Rendering;

using Tunnerer.Core.Entities;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;
using Tunnerer.Desktop.Gui;

public sealed class RenderViewState
{
    private int _nativeOverBudgetFrames;
    private int _nativeUnderBudgetFrames;

    public Size HiResSize { get; private set; }
    public int CamPixelX { get; private set; }
    public int CamPixelY { get; private set; }
    public Rect LastAuxViewport { get; private set; }
    public int NativeContinuousSampleCount { get; private set; } = DesktopScreenTweaks.NativeContinuousSampleHigh;

    public Size ComputeViewSize(int windowW, int windowH, bool supportsUi)
    {
        int viewportHeight = supportsUi
            ? Math.Max(1, windowH - (int)GameHud.BottomPanelHeight)
            : Math.Max(1, windowH);
        return new Size(Math.Max(1, windowW), viewportHeight);
    }

    public void EnsureHiResBuffer(Size size)
    {
        if (HiResSize == size)
            return;

        HiResSize = size;
    }

    public void UpdateCamera(Tank? player, Size terrainSize, int viewW, int viewH, int scale)
    {
        int worldPixelW = terrainSize.X * scale;
        int worldPixelH = terrainSize.Y * scale;

        if (player != null)
        {
            int centerPx = (int)MathF.Round(player.Position.X * scale);
            int centerPy = (int)MathF.Round(player.Position.Y * scale);
            CamPixelX = centerPx - viewW / 2;
            CamPixelY = centerPy - viewH / 2;
        }

        if (viewW >= worldPixelW)
            CamPixelX = -(viewW - worldPixelW) / 2;
        else
            CamPixelX = Math.Clamp(CamPixelX, 0, worldPixelW - viewW);

        if (viewH >= worldPixelH)
            CamPixelY = -(viewH - worldPixelH) / 2;
        else
            CamPixelY = Math.Clamp(CamPixelY, 0, worldPixelH - viewH);
    }

    public Rect ComputeVisibleWorldRect(Size terrainSize, int scale)
    {
        int viewMinX = Math.Max(0, CamPixelX / scale);
        int viewMinY = Math.Max(0, CamPixelY / scale);
        int viewW = (HiResSize.X + scale - 1) / scale;
        int viewH = (HiResSize.Y + scale - 1) / scale;
        int viewMaxX = Math.Min(terrainSize.X, viewMinX + viewW);
        int viewMaxY = Math.Min(terrainSize.Y, viewMinY + viewH);
        return new Rect(viewMinX, viewMinY, viewMaxX - viewMinX, viewMaxY - viewMinY);
    }

    public static Rect? ClampToViewport(Rect? dirty, in Rect viewport)
    {
        if (dirty is null)
            return null;
        return RectMath.Intersect(dirty.Value, viewport);
    }

    public Rect? ComputeCameraRevealRect(in Rect currentViewport)
    {
        if (LastAuxViewport.Width == 0 || LastAuxViewport.Height == 0)
            return currentViewport;

        if (currentViewport.Left == LastAuxViewport.Left &&
            currentViewport.Top == LastAuxViewport.Top &&
            currentViewport.Right == LastAuxViewport.Right &&
            currentViewport.Bottom == LastAuxViewport.Bottom)
        {
            return null;
        }

        Rect? reveal = null;

        if (currentViewport.Left < LastAuxViewport.Left)
            reveal = RectMath.Union(reveal, new Rect(currentViewport.Left, currentViewport.Top,
                LastAuxViewport.Left - currentViewport.Left, currentViewport.Height));

        if (currentViewport.Right > LastAuxViewport.Right)
            reveal = RectMath.Union(reveal, new Rect(LastAuxViewport.Right, currentViewport.Top,
                currentViewport.Right - LastAuxViewport.Right, currentViewport.Height));

        if (currentViewport.Top < LastAuxViewport.Top)
            reveal = RectMath.Union(reveal, new Rect(currentViewport.Left, currentViewport.Top,
                currentViewport.Width, LastAuxViewport.Top - currentViewport.Top));

        if (currentViewport.Bottom > LastAuxViewport.Bottom)
            reveal = RectMath.Union(reveal, new Rect(currentViewport.Left, LastAuxViewport.Bottom,
                currentViewport.Width, currentViewport.Bottom - LastAuxViewport.Bottom));

        return reveal;
    }

    public void UpdateLastAuxViewport(in Rect viewport)
    {
        LastAuxViewport = viewport;
    }

    public static Rect? TryGetDirtyCellBounds(IReadOnlyList<Position> dirtyCells)
    {
        if (dirtyCells.Count == 0)
            return null;

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            Position p = dirtyCells[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return RectMath.FromMinMaxInclusive(minX, minY, maxX, maxY);
    }

    public static Rect? MergeDirtyRects(Rect? a, Rect? b)
    {
        return RectMath.Union(a, b);
    }

    public static Rect? SubtractCoveredHeatRect(Rect? heat, Rect? full)
    {
        if (heat is null || full is null)
            return heat;

        Rect h = heat.Value;
        Rect f = full.Value;
        if (h.Left >= f.Left && h.Top >= f.Top && h.Right <= f.Right && h.Bottom <= f.Bottom)
            return null;

        return heat;
    }

    public void UpdateNativeContinuousQuality(double frameMs)
    {
        float budget = DesktopScreenTweaks.NativeContinuousRenderBudgetMs;
        int hysteresis = DesktopScreenTweaks.NativeContinuousBudgetHysteresisFrames;
        if (frameMs > budget)
        {
            _nativeOverBudgetFrames++;
            _nativeUnderBudgetFrames = 0;
        }
        else if (frameMs < budget * DesktopScreenTweaks.NativeContinuousBudgetUnderThreshold)
        {
            _nativeUnderBudgetFrames++;
            _nativeOverBudgetFrames = 0;
        }
        else
        {
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
        }

        if (_nativeOverBudgetFrames >= hysteresis && NativeContinuousSampleCount > DesktopScreenTweaks.NativeContinuousSampleLow)
        {
            NativeContinuousSampleCount = NativeContinuousSampleCount >= DesktopScreenTweaks.NativeContinuousSampleHigh
                ? DesktopScreenTweaks.NativeContinuousSampleMedium
                : DesktopScreenTweaks.NativeContinuousSampleLow;
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
            Console.WriteLine($"[Render] Native continuous sample count reduced to {NativeContinuousSampleCount}");
        }
        else if (_nativeUnderBudgetFrames >= hysteresis * DesktopScreenTweaks.NativeContinuousRecoveryFramesMultiplier &&
                 NativeContinuousSampleCount < DesktopScreenTweaks.NativeContinuousSampleHigh)
        {
            NativeContinuousSampleCount = NativeContinuousSampleCount <= DesktopScreenTweaks.NativeContinuousSampleLow
                ? DesktopScreenTweaks.NativeContinuousSampleMedium
                : DesktopScreenTweaks.NativeContinuousSampleHigh;
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
            Console.WriteLine($"[Render] Native continuous sample count increased to {NativeContinuousSampleCount}");
        }
    }
}
