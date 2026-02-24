namespace TunnelTanks.Desktop.Input;

using Silk.NET.SDL;
using TunnelTanks.Core.Input;
using TunnelTanks.Core.Types;

public unsafe class KeyboardController
{
    private readonly Sdl _sdl;
    private readonly Scancode _left, _right, _up, _down, _shoot;

    public KeyboardController(Sdl sdl, Scancode left, Scancode right, Scancode up, Scancode down, Scancode shoot)
    {
        _sdl = sdl;
        _left = left; _right = right; _up = up; _down = down; _shoot = shoot;
    }

    public ControllerOutput Poll()
    {
        byte* keys = _sdl.GetKeyboardState(null);
        int dx = 0, dy = 0;
        if (keys[(int)_left] != 0) dx -= 1;
        if (keys[(int)_right] != 0) dx += 1;
        if (keys[(int)_up] != 0) dy -= 1;
        if (keys[(int)_down] != 0) dy += 1;

        return new ControllerOutput
        {
            MoveSpeed = new Offset(dx, dy),
            ShootPrimary = keys[(int)_shoot] != 0,
        };
    }
}
