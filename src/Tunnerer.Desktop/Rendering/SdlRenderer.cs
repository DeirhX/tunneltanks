namespace Tunnerer.Desktop.Rendering;

using Silk.NET.SDL;
using Tunnerer.Core.Types;
using System.Runtime.InteropServices;

/// <summary>
/// SDL2 window with an OpenGL context. No SDL_Renderer — all drawing goes through GL.
/// </summary>
public sealed unsafe class SdlRenderer : IDisposable
{
    private readonly Sdl _sdl;
    private Window* _window;
    private void* _glContext;
    private readonly Size _windowSize;
    private bool _disposed;

    public Sdl Sdl => _sdl;
    public Window* NativeWindow => _window;

    public SdlRenderer(string title, Size windowSize)
    {
        _windowSize = windowSize;
        _sdl = Sdl.GetApi();

        if (_sdl.Init(Sdl.InitVideo | Sdl.InitEvents) < 0)
            throw new Exception($"SDL_Init failed: {Marshal.PtrToStringAnsi((nint)_sdl.GetError())}");

        _sdl.GLSetAttribute(GLattr.ContextMajorVersion, 3);
        _sdl.GLSetAttribute(GLattr.ContextMinorVersion, 3);
        _sdl.GLSetAttribute(GLattr.ContextProfileMask, (int)GLprofile.Core);
        _sdl.GLSetAttribute(GLattr.Doublebuffer, 1);

        _window = _sdl.CreateWindow(
            title,
            Sdl.WindowposCentered, Sdl.WindowposCentered,
            windowSize.X, windowSize.Y,
            (uint)(WindowFlags.Shown | WindowFlags.Resizable | WindowFlags.Opengl));

        if (_window == null)
            throw new Exception($"SDL_CreateWindow failed: {Marshal.PtrToStringAnsi((nint)_sdl.GetError())}");

        _glContext = _sdl.GLCreateContext(_window);
        if (_glContext == null)
            throw new Exception($"SDL_GL_CreateContext failed: {Marshal.PtrToStringAnsi((nint)_sdl.GetError())}");

        _sdl.GLMakeCurrent(_window, _glContext);
        _sdl.GLSetSwapInterval(1);
    }

    public (int w, int h) GetWindowSize()
    {
        int w, h;
        _sdl.GetWindowSize(_window, &w, &h);
        return (w, h);
    }

    public (int x, int y, uint buttons) GetMouseState()
    {
        int mx, my;
        uint buttons = _sdl.GetMouseState(&mx, &my);
        return (mx, my, buttons);
    }

    public void SwapWindow()
    {
        _sdl.GLSwapWindow(_window);
    }

    /// <summary>
    /// Polls SDL events. Returns false if quit was requested.
    /// Calls <paramref name="eventHandler"/> for each event before checking quit.
    /// </summary>
    public bool PollEvents(Action<Event>? eventHandler = null)
    {
        Event ev;
        while (_sdl.PollEvent(&ev) != 0)
        {
            eventHandler?.Invoke(ev);

            if (ev.Type == (uint)EventType.Quit)
                return false;
            if (ev.Type == (uint)EventType.Keydown && ev.Key.Keysym.Sym == (int)KeyCode.KEscape)
                return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_glContext != null) _sdl.GLDeleteContext(_glContext);
        if (_window != null) _sdl.DestroyWindow(_window);
        _sdl.Quit();
        _sdl.Dispose();
    }
}
