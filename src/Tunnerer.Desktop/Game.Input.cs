namespace Tunnerer.Desktop;

using Silk.NET.SDL;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;

public partial class Game
{
    private void HandleEvent(Event ev)
    {
        _renderBackend.ProcessEvent(ev);
        if (ev.Type != (uint)EventType.Keydown)
            return;

        // Ignore key-repeat so toggles flip once per physical press.
        if (ev.Key.Repeat != 0)
            return;

        if ((Scancode)ev.Key.Keysym.Scancode == Scancode.ScancodeF9)
        {
            _showThermalRegionDebug = !_showThermalRegionDebug;
            Console.WriteLine($"[Debug] Thermal regions overlay: {(_showThermalRegionDebug ? "ON" : "OFF")} (F9)");
        }

        if ((Scancode)ev.Key.Keysym.Scancode == Scancode.Scancode1)
        {
            _showHeatDebugOverlay = !_showHeatDebugOverlay;
            Console.WriteLine($"[Debug] Heat overlay: {(_showHeatDebugOverlay ? "ON" : "OFF")} (1)");
        }

        if ((Scancode)ev.Key.Keysym.Scancode == Scancode.ScancodeF10)
        {
            _renderBackend.RequestScreenshot("postps");
            Console.WriteLine("[Debug] Screenshot requested (F10).");
        }
    }

    private DirectionF? ComputeAimDirection(IReadOnlyList<Core.Entities.Tank> tanks)
    {
        if (tanks.Count == 0) return null;
        var tank = tanks[0];

        if (!IsMouseInViewport(out float relX, out float relY))
            return null;

        int scale = DesktopScreenTweaks.PixelScale;
        float aimWorldX = (_camPixelX + relX) / scale;
        float aimWorldY = (_camPixelY + relY) / scale;

        float dx = aimWorldX - tank.Position.X;
        float dy = aimWorldY - tank.Position.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return null;
        return new DirectionF(dx / len, dy / len);
    }

    private bool IsMouseInViewport(out float relX, out float relY)
    {
        var (mx, my, _) = _renderer.GetMouseState();
        (float x, float y, float w, float h) vp = _renderBackend.SupportsUi
            ? _hud.ViewportRect
            : (0f, 0f, _hiResSize.X, _hiResSize.Y);

        relX = 0; relY = 0;
        if (vp.w <= 0 || vp.h <= 0) return false;

        relX = mx - vp.x;
        relY = my - vp.y;
        if (relX < 0 || relY < 0 || relX >= vp.w || relY >= vp.h)
            return false;

        if (_renderBackend.SupportsUi)
        {
            float sx = _hiResSize.X / Math.Max(1f, vp.w);
            float sy = _hiResSize.Y / Math.Max(1f, vp.h);
            relX *= sx;
            relY *= sy;
        }

        return true;
    }

    private bool IsLeftMouseDown()
    {
        var (_, _, buttons) = _renderer.GetMouseState();
        return (buttons & 1) != 0;
    }
}
