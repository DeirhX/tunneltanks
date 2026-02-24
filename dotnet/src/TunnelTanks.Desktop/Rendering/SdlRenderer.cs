namespace TunnelTanks.Desktop.Rendering;

using Silk.NET.SDL;
using TunnelTanks.Core.Types;
using System.Runtime.InteropServices;

public sealed unsafe class SdlRenderer : IDisposable
{
    private readonly Sdl _sdl;
    private Window* _window;
    private Renderer* _renderer;
    private Texture* _texture;
    private readonly Size _renderSize;
    private bool _disposed;

    public Size RenderSize => _renderSize;

    public SdlRenderer(string title, Size windowSize, Size renderSize)
    {
        _renderSize = renderSize;
        _sdl = Sdl.GetApi();

        if (_sdl.Init(Sdl.InitVideo | Sdl.InitEvents) < 0)
            throw new Exception($"SDL_Init failed: {Marshal.PtrToStringAnsi((nint)_sdl.GetError())}");

        _window = _sdl.CreateWindow(
            title,
            Sdl.WindowposCentered, Sdl.WindowposCentered,
            windowSize.X, windowSize.Y,
            (uint)(WindowFlags.Shown | WindowFlags.Resizable));

        if (_window == null)
            throw new Exception($"SDL_CreateWindow failed: {Marshal.PtrToStringAnsi((nint)_sdl.GetError())}");

        _renderer = _sdl.CreateRenderer(_window, -1,
            (uint)(RendererFlags.Accelerated | RendererFlags.Presentvsync));

        if (_renderer == null)
            throw new Exception($"SDL_CreateRenderer failed: {Marshal.PtrToStringAnsi((nint)_sdl.GetError())}");

        _sdl.SetHint("SDL_RENDER_SCALE_QUALITY", "0");
        _sdl.RenderSetLogicalSize(_renderer, renderSize.X, renderSize.Y);

        _texture = _sdl.CreateTexture(_renderer,
            (uint)Silk.NET.SDL.PixelFormatEnum.Argb8888,
            (int)TextureAccess.Streaming,
            renderSize.X, renderSize.Y);

        if (_texture == null)
            throw new Exception($"SDL_CreateTexture failed: {Marshal.PtrToStringAnsi((nint)_sdl.GetError())}");
    }

    public (int x, int y, uint buttons) GetMouseState()
    {
        int mx, my;
        uint buttons = _sdl.GetMouseState(&mx, &my);
        int winW, winH;
        _sdl.GetWindowSize(_window, &winW, &winH);
        int rx = winW > 0 ? mx * _renderSize.X / winW : 0;
        int ry = winH > 0 ? my * _renderSize.Y / winH : 0;
        return (rx, ry, buttons);
    }

    public void RenderFrame(uint[] pixels)
    {
        fixed (uint* ptr = pixels)
        {
            _sdl.UpdateTexture(_texture, null, ptr, _renderSize.X * sizeof(uint));
        }
        _sdl.RenderClear(_renderer);
        _sdl.RenderCopy(_renderer, _texture, null, null);
        _sdl.RenderPresent(_renderer);
    }

    public bool PollEvents()
    {
        Event ev;
        while (_sdl.PollEvent(&ev) != 0)
        {
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

        if (_texture != null) _sdl.DestroyTexture(_texture);
        if (_renderer != null) _sdl.DestroyRenderer(_renderer);
        if (_window != null) _sdl.DestroyWindow(_window);
        _sdl.Quit();
        _sdl.Dispose();
    }
}
