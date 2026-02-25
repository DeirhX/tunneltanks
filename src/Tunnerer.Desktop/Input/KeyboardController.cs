namespace Tunnerer.Desktop.Input;

using Silk.NET.SDL;
using Tunnerer.Core.Input;
using Tunnerer.Core.Types;

public readonly record struct KeyBindings(
    Scancode Left, Scancode Right, Scancode Up, Scancode Down, Scancode Shoot);

public unsafe class KeyboardController
{
    private readonly Sdl _sdl;
    private readonly KeyBindings _keys;

    public KeyboardController(Sdl sdl, KeyBindings keys)
    {
        _sdl = sdl;
        _keys = keys;
    }

    public ControllerOutput Poll()
    {
        byte* keys = _sdl.GetKeyboardState(null);
        int dx = 0, dy = 0;
        if (keys[(int)_keys.Left] != 0) dx -= 1;
        if (keys[(int)_keys.Right] != 0) dx += 1;
        if (keys[(int)_keys.Up] != 0) dy -= 1;
        if (keys[(int)_keys.Down] != 0) dy += 1;

        return new ControllerOutput
        {
            MoveSpeed = new Offset(dx, dy),
            ShootPrimary = keys[(int)_keys.Shoot] != 0,
        };
    }
}
