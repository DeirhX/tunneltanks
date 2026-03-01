namespace Tunnerer.Desktop.Rendering.Dx11;

using Silk.NET.SDL;
using System.Runtime.InteropServices;

public sealed unsafe partial class Backend
{
    private void InitSdlFallback()
    {
        Environment.SetEnvironmentVariable("SDL_RENDER_DRIVER", "direct3d11");
        _sdlRenderer = _sdl.CreateRenderer(_window, -1, (uint)(RendererFlags.Accelerated | RendererFlags.Presentvsync));
        if (_sdlRenderer == null)
        {
            Environment.SetEnvironmentVariable("SDL_RENDER_DRIVER", null);
            _sdlRenderer = _sdl.CreateRenderer(_window, -1, (uint)(RendererFlags.Accelerated | RendererFlags.Presentvsync));
        }
        if (_sdlRenderer == null)
            throw new Exception("Failed to create SDL renderer for DX11 fallback.");

        _sdl.RenderSetIntegerScale(_sdlRenderer, SdlBool.True);
        LogSdlRendererInfo();
        Console.WriteLine("[Render] DX11 using SDL accelerated renderer fallback.");
    }

    private void UploadSdl(int w, int h)
    {
        EnsureSdlFrameTexture(w, h);
        fixed (uint* ptr = _processedPixels)
        {
            _sdl.UpdateTexture(_sdlFrameTexture, null, ptr, w * sizeof(uint));
        }
    }

    private void EnsureSdlFrameTexture(int w, int h)
    {
        if (_sdlFrameTexture != null && _sdlFrameW == w && _sdlFrameH == h)
            return;
        if (_sdlFrameTexture != null) { _sdl.DestroyTexture(_sdlFrameTexture); _sdlFrameTexture = null; }

        _sdlFrameTexture = _sdl.CreateTexture(
            _sdlRenderer, (uint)PixelFormatEnum.Argb8888, (int)TextureAccess.Streaming, w, h);
        if (_sdlFrameTexture == null)
            throw new Exception("Failed to create SDL frame texture.");
        _sdl.SetTextureBlendMode(_sdlFrameTexture, BlendMode.None);
        _sdlFrameW = w;
        _sdlFrameH = h;
    }

    private void LogSdlRendererInfo()
    {
        RendererInfo info = default;
        if (_sdl.GetRendererInfo(_sdlRenderer, &info) != 0) return;
        string driver = Marshal.PtrToStringAnsi((nint)info.Name) ?? "unknown";
        Console.WriteLine($"[Render] SDL renderer driver={driver}");
    }
}
