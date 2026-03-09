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
        if (GameCommandController.TryTranslateHotkey(scancode, out GameCommand command))
            ExecuteGameCommand(command, "input");
    }

    private void ExecuteGameCommand(GameCommand command, string source)
    {
        RecordInputCommandForReplay(command, source);
        _commandController.Execute(
            command,
            source,
            requestScreenshot: label => _renderBackend.RequestScreenshot(label),
            toggleInputRecording: ToggleInputRecording);
    }

    private DirectionF? ComputeAimDirection(IReadOnlyList<Core.Entities.Tank> tanks)
    {
        if (tanks.Count == 0) return null;
        var tank = tanks[0];

        if (!IsMouseInViewport(out float relX, out float relY))
            return null;

        int scale = DesktopScreenTweaks.PixelScale;
        float aimWorldX = (_renderViewState.CamPixelX + relX) / scale;
        float aimWorldY = (_renderViewState.CamPixelY + relY) / scale;

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
            : (0f, 0f, _renderViewState.HiResSize.X, _renderViewState.HiResSize.Y);

        relX = 0; relY = 0;
        if (vp.w <= 0 || vp.h <= 0) return false;

        relX = mx - vp.x;
        relY = my - vp.y;
        if (relX < 0 || relY < 0 || relX >= vp.w || relY >= vp.h)
            return false;

        if (_renderBackend.SupportsUi)
        {
            float sx = _renderViewState.HiResSize.X / Math.Max(1f, vp.w);
            float sy = _renderViewState.HiResSize.Y / Math.Max(1f, vp.h);
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
