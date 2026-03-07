namespace Tunnerer.Desktop;

using Silk.NET.SDL;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;
using Tunnerer.Desktop.Rendering;

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

        HandleHotkey((Scancode)ev.Key.Keysym.Scancode);
    }

    private void HandleHotkey(Scancode scancode)
    {
        switch (scancode)
        {
            case Scancode.ScancodeF9:
                ToggleWithLog(ref _showThermalRegionDebug, "Debug", "Thermal regions overlay", "F9");
                break;
            case Scancode.Scancode0:
                ToggleWithLog(ref _showHeatDebugOverlay, "Debug", "Heat overlay", "0");
                break;
            case Scancode.Scancode1:
                TogglePassWithLog(PostProcessPassFlags.Bloom, "Bloom", "1");
                break;
            case Scancode.Scancode2:
                TogglePassWithLog(PostProcessPassFlags.Vignette, "Vignette", "2");
                break;
            case Scancode.Scancode3:
                TogglePassWithLog(PostProcessPassFlags.EdgeLift, "Edge lift", "3");
                break;
            case Scancode.Scancode4:
                TogglePassWithLog(PostProcessPassFlags.TerrainCurve, "Terrain curve", "4");
                break;
            case Scancode.Scancode5:
                TogglePassWithLog(PostProcessPassFlags.TerrainAux, "Terrain aux", "5");
                break;
            case Scancode.Scancode6:
                TogglePassWithLog(PostProcessPassFlags.TankGlow, "Tank glow", "6");
                break;
            case Scancode.ScancodeF10:
                _renderBackend.RequestScreenshot("postps");
                Console.WriteLine("[Debug] Screenshot requested (F10).");
                break;
            case Scancode.ScancodeF11:
                ToggleWithLog(ref _showPostPassOverlay, "Debug", "Post pass overlay", "F11");
                break;
        }
    }

    private static void ToggleWithLog(ref bool flag, string group, string label, string key)
    {
        flag = !flag;
        Console.WriteLine($"[{group}] {label}: {(flag ? "ON" : "OFF")} ({key})");
    }

    private void TogglePassWithLog(PostProcessPassFlags pass, string label, string key)
    {
        _enabledPostPasses ^= pass;
        bool enabled = (_enabledPostPasses & pass) != 0;
        Console.WriteLine($"[PostPs] {label}: {(enabled ? "ON" : "OFF")} ({key})");
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
