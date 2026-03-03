namespace Tunnerer.Desktop;

using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;

public partial class Game
{
    private void UpdateCamera(Core.Entities.Tank? player, int viewW, int viewH, int scale)
    {
        int worldPixelW = _terrainSize.X * scale;
        int worldPixelH = _terrainSize.Y * scale;

        if (player != null)
        {
            int centerPx = (int)MathF.Round(player.Position.X * scale);
            int centerPy = (int)MathF.Round(player.Position.Y * scale);
            _camPixelX = centerPx - viewW / 2;
            _camPixelY = centerPy - viewH / 2;
        }

        if (viewW >= worldPixelW)
            _camPixelX = -(viewW - worldPixelW) / 2;
        else
            _camPixelX = Math.Clamp(_camPixelX, 0, worldPixelW - viewW);

        if (viewH >= worldPixelH)
            _camPixelY = -(viewH - worldPixelH) / 2;
        else
            _camPixelY = Math.Clamp(_camPixelY, 0, worldPixelH - viewH);
    }

    private void EnsureHiResBuffer(Size size)
    {
        if (_hiResSize == size)
            return;

        _hiResSize = size;
    }

    private void UpdateNativeContinuousQuality(double frameMs)
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

        if (_nativeOverBudgetFrames >= hysteresis && _nativeContinuousSampleCount > DesktopScreenTweaks.NativeContinuousSampleLow)
        {
            _nativeContinuousSampleCount = _nativeContinuousSampleCount >= DesktopScreenTweaks.NativeContinuousSampleHigh
                ? DesktopScreenTweaks.NativeContinuousSampleMedium
                : DesktopScreenTweaks.NativeContinuousSampleLow;
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
            Console.WriteLine($"[Render] Native continuous sample count reduced to {_nativeContinuousSampleCount}");
        }
        else if (_nativeUnderBudgetFrames >= hysteresis * DesktopScreenTweaks.NativeContinuousRecoveryFramesMultiplier &&
                 _nativeContinuousSampleCount < DesktopScreenTweaks.NativeContinuousSampleHigh)
        {
            _nativeContinuousSampleCount = _nativeContinuousSampleCount <= DesktopScreenTweaks.NativeContinuousSampleLow
                ? DesktopScreenTweaks.NativeContinuousSampleMedium
                : DesktopScreenTweaks.NativeContinuousSampleHigh;
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
            Console.WriteLine($"[Render] Native continuous sample count increased to {_nativeContinuousSampleCount}");
        }
    }
}
