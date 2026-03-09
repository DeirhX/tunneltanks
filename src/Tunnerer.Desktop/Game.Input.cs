namespace Tunnerer.Desktop;

using Silk.NET.SDL;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;
using Tunnerer.Desktop.Input;
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

        Scancode scancode = (Scancode)ev.Key.Keysym.Scancode;
        if (TryTranslateHotkey(scancode, out GameCommand command))
            ExecuteGameCommand(command, "input");
    }

    private static bool TryTranslateHotkey(Scancode scancode, out GameCommand command)
    {
        switch (scancode)
        {
            case Scancode.ScancodeF9:
                command = GameCommand.ToggleThermalRegionsOverlay;
                return true;
            case Scancode.Scancode0:
                command = GameCommand.ToggleHeatOverlay;
                return true;
            case Scancode.Scancode1:
                command = GameCommand.TogglePostBloom;
                return true;
            case Scancode.Scancode2:
                command = GameCommand.TogglePostVignette;
                return true;
            case Scancode.Scancode3:
                command = GameCommand.TogglePostEdgeLift;
                return true;
            case Scancode.Scancode4:
                command = GameCommand.TogglePostTerrainCurve;
                return true;
            case Scancode.Scancode5:
                command = GameCommand.TogglePostTerrainAux;
                return true;
            case Scancode.Scancode6:
                command = GameCommand.TogglePostTankGlow;
                return true;
            case Scancode.Scancode7:
                command = GameCommand.TogglePostNativeTerrainSmoothing;
                return true;
            case Scancode.Scancode8:
                command = GameCommand.TogglePostTerrainHeat;
                return true;
            case Scancode.ScancodeF10:
                command = GameCommand.RequestScreenshot;
                return true;
            case Scancode.ScancodeF11:
                command = GameCommand.TogglePostPassOverlay;
                return true;
            default:
                command = default;
                return false;
        }
    }

    private void ExecuteGameCommand(GameCommand command, string source)
    {
        switch (command)
        {
            case GameCommand.ToggleThermalRegionsOverlay:
                ToggleWithLog(ref _showThermalRegionDebug, "Debug", "Thermal regions overlay", source);
                return;
            case GameCommand.ToggleHeatOverlay:
                ToggleWithLog(ref _showHeatDebugOverlay, "Debug", "Heat overlay", source);
                return;
            case GameCommand.TogglePostBloom:
                TogglePassWithLog(PostProcessPassFlags.Bloom, "Bloom", source);
                return;
            case GameCommand.TogglePostVignette:
                TogglePassWithLog(PostProcessPassFlags.Vignette, "Vignette", source);
                return;
            case GameCommand.TogglePostEdgeLift:
                TogglePassWithLog(PostProcessPassFlags.EdgeLift, "Edge lift", source);
                return;
            case GameCommand.TogglePostTerrainCurve:
                TogglePassWithLog(PostProcessPassFlags.TerrainCurve, "Terrain curve", source);
                return;
            case GameCommand.TogglePostTerrainAux:
                TogglePassWithLog(PostProcessPassFlags.TerrainAux, "Terrain texture", source);
                return;
            case GameCommand.TogglePostTerrainHeat:
                TogglePassWithLog(PostProcessPassFlags.TerrainHeat, "Terrain heat", source);
                return;
            case GameCommand.TogglePostTankGlow:
                TogglePassWithLog(PostProcessPassFlags.TankGlow, "Tank glow", source);
                return;
            case GameCommand.TogglePostNativeTerrainSmoothing:
                TogglePassWithLog(PostProcessPassFlags.NativeTerrainSmoothing, "Native terrain smoothing", source);
                return;
            case GameCommand.RequestScreenshot:
                _renderBackend.RequestScreenshot("postps");
                Console.WriteLine($"[Debug] Screenshot requested [source={source}].");
                return;
            case GameCommand.TogglePostPassOverlay:
                ToggleWithLog(ref _showPostPassOverlay, "Debug", "Post pass overlay", source);
                return;
            default:
                return;
        }
    }

    private static void ToggleWithLog(ref bool flag, string group, string label, string source)
    {
        flag = !flag;
        Console.WriteLine($"[{group}] {label}: {(flag ? "ON" : "OFF")} [source={source}]");
    }

    private void TogglePassWithLog(PostProcessPassFlags pass, string label, string source)
    {
        _enabledPostPasses ^= pass;
        bool enabled = (_enabledPostPasses & pass) != 0;
        Console.WriteLine($"[PostPs] {label}: {(enabled ? "ON" : "OFF")} [source={source}]");
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
