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
        bool shiftHeld = (ev.Key.Keysym.Mod & (ushort)Keymod.Shift) != 0;
        if (GameCommandController.TryTranslateHotkey(scancode, shiftHeld, out GameCommand command))
            ExecuteGameCommand(command, InputCommandSources.Input);
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

    private FrameInputSnapshot CaptureFrameInput()
    {
        var (mx, my, buttons) = _renderer.GetMouseState();
        return new FrameInputSnapshot(mx, my, buttons);
    }

    private DirectionF? ComputeAimDirection(IReadOnlyList<Core.Entities.Tank> tanks, in FrameInputSnapshot frameInput)
    {
        if (tanks.Count == 0) return null;
        var tank = tanks[0];

        if (!IsMouseInViewport(frameInput, out float relX, out float relY))
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

    private bool IsMouseInViewport(in FrameInputSnapshot frameInput, out float relX, out float relY)
    {
        (float x, float y, float w, float h) vp = _renderBackend.SupportsUi
            ? _hud.ViewportRect
            : (0f, 0f, _renderViewState.HiResSize.X, _renderViewState.HiResSize.Y);

        relX = 0; relY = 0;
        if (vp.w <= 0 || vp.h <= 0) return false;

        relX = frameInput.MouseX - vp.x;
        relY = frameInput.MouseY - vp.y;
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
}
