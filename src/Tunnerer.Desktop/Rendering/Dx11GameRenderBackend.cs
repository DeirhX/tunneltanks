namespace Tunnerer.Desktop.Rendering;

using Silk.NET.SDL;
using Tunnerer.Core.Config;
using Tunnerer.Core.Types;

/// <summary>
/// DX11 migration backend currently using SDL's accelerated renderer path.
/// This provides visible output while dedicated D3D11 device/swapchain code is implemented.
/// </summary>
public sealed unsafe class Dx11GameRenderBackend : IGameRenderBackend
{
    private readonly Sdl _sdl;
    private readonly Window* _window;
    private Renderer* _renderer;
    private Texture* _frameTexture;
    private uint[] _fallbackPixels = Array.Empty<uint>();
    private int _frameW;
    private int _frameH;
    private bool _disposed;

    public nint GameTextureId => nint.Zero;
    public bool SupportsUi => false;

    public Dx11GameRenderBackend(Sdl sdl, Window* window)
    {
        _sdl = sdl;
        _window = window;
        _renderer = _sdl.CreateRenderer(_window, -1, (uint)(RendererFlags.Accelerated | RendererFlags.Presentvsync));
        if (_renderer == null)
            throw new Exception("Failed to create SDL accelerated renderer for DX11 path.");

        _sdl.RenderSetIntegerScale(_renderer, SdlBool.True);
        Console.WriteLine("[Render] DX11 backend using SDL accelerated renderer fallback.");
    }

    public void ProcessEvent(Event ev)
    {
        _ = ev;
    }

    public void UploadGamePixels(in GamePixelsUpload upload)
    {
        int width = upload.View.ViewSize.X;
        int height = upload.View.ViewSize.Y;
        EnsureFrameTexture(width, height);
        EnsureFallbackPixels(width * height);
        Array.Copy(upload.Pixels, _fallbackPixels, _fallbackPixels.Length);
        ApplyFallbackHeatEffects(_fallbackPixels, upload);

        fixed (uint* ptr = _fallbackPixels)
        {
            int pitch = width * sizeof(uint);
            if (_sdl.UpdateTexture(_frameTexture, null, ptr, pitch) != 0)
                throw new Exception("SDL_UpdateTexture failed in DX11 backend.");
        }
    }

    public void ClearFrame(Size viewportSize, Tunnerer.Core.Types.Color clearColor)
    {
        _ = viewportSize;
        _sdl.SetRenderDrawColor(_renderer, clearColor.R, clearColor.G, clearColor.B, clearColor.A);
        _sdl.RenderClear(_renderer);
    }

    public void NewFrame(int windowW, int windowH, float deltaTime)
    {
        _ = windowW;
        _ = windowH;
        _ = deltaTime;
    }

    public void Render()
    {
        if (_frameTexture != null)
            _sdl.RenderCopy(_renderer, _frameTexture, null, null);

        DrawMouseCrosshair();
        _sdl.RenderPresent(_renderer);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_frameTexture != null)
        {
            _sdl.DestroyTexture(_frameTexture);
            _frameTexture = null;
        }
        if (_renderer != null)
        {
            _sdl.DestroyRenderer(_renderer);
            _renderer = null;
        }
    }

    private void EnsureFrameTexture(int width, int height)
    {
        if (_frameTexture != null && _frameW == width && _frameH == height)
            return;

        if (_frameTexture != null)
        {
            _sdl.DestroyTexture(_frameTexture);
            _frameTexture = null;
        }

        _frameTexture = _sdl.CreateTexture(
            _renderer,
            (uint)PixelFormatEnum.Argb8888,
            (int)TextureAccess.Streaming,
            width,
            height);
        if (_frameTexture == null)
            throw new Exception("Failed to create SDL frame texture for DX11 backend.");

        _sdl.SetTextureBlendMode(_frameTexture, BlendMode.None);
        _frameW = width;
        _frameH = height;
    }

    private void EnsureFallbackPixels(int count)
    {
        if (_fallbackPixels.Length != count)
            _fallbackPixels = new uint[count];
    }

    private static void ApplyFallbackHeatEffects(uint[] pixels, in GamePixelsUpload upload)
    {
        ApplyTerrainHeatGlow(pixels, upload);
        ApplyTankHeatGlow(pixels, upload);
    }

    private static void ApplyTerrainHeatGlow(uint[] pixels, in GamePixelsUpload upload)
    {
        var aux = upload.TerrainAux;
        int worldW = upload.View.WorldSize.X;
        int worldH = upload.View.WorldSize.Y;
        int viewW = upload.View.ViewSize.X;
        int viewH = upload.View.ViewSize.Y;
        int scale = upload.View.PixelScale;
        if (aux == null || worldW <= 0 || worldH <= 0 || scale <= 0)
            return;

        int camX = upload.View.CameraPixels.X;
        int camY = upload.View.CameraPixels.Y;
        float threshold = Tweaks.Screen.PostTerrainHeatThreshold;
        int glowR = (int)(Tweaks.Screen.PostTerrainHeatGlowR * 255f);
        int glowG = (int)(Tweaks.Screen.PostTerrainHeatGlowG * 255f);
        int glowB = (int)(Tweaks.Screen.PostTerrainHeatGlowB * 255f);

        for (int py = 0; py < viewH; py++)
        {
            int worldY = (py + camY) / scale;
            if ((uint)worldY >= (uint)worldH) continue;
            int row = py * viewW;
            int worldRow = worldY * worldW;
            for (int px = 0; px < viewW; px++)
            {
                int worldX = (px + camX) / scale;
                if ((uint)worldX >= (uint)worldW) continue;
                int auxIdx = (worldRow + worldX) * 4;
                float heat = aux[auxIdx] / 255f;
                if (heat <= threshold) continue;

                float t = (heat - threshold) / MathF.Max(0.0001f, 1f - threshold);
                int addR = (int)(glowR * t * 0.35f);
                int addG = (int)(glowG * t * 0.35f);
                int addB = (int)(glowB * t * 0.35f);
                pixels[row + px] = Additive(pixels[row + px], addR, addG, addB);
            }
        }
    }

    private static void ApplyTankHeatGlow(uint[] pixels, in GamePixelsUpload upload)
    {
        var data = upload.TankHeatGlowData;
        int count = upload.TankHeatGlowCount;
        int viewW = upload.View.ViewSize.X;
        int viewH = upload.View.ViewSize.Y;
        if (data == null || count <= 0 || viewW <= 0 || viewH <= 0)
            return;

        float colorR = Tweaks.Screen.PostTankHeatGlowR * 255f;
        float colorG = Tweaks.Screen.PostTankHeatGlowG * 255f;
        float colorB = Tweaks.Screen.PostTankHeatGlowB * 255f;
        float maxDim = MathF.Max(viewW, viewH);

        for (int i = 0; i < count; i++)
        {
            int baseIdx = i * 4;
            float cx = data[baseIdx + 0] * viewW;
            float cy = data[baseIdx + 1] * viewH;
            float radius = data[baseIdx + 2] * maxDim;
            float intensity = Math.Clamp(data[baseIdx + 3], 0f, 1f);
            if (radius <= 0.01f || intensity <= 0.001f) continue;

            int minX = Math.Max(0, (int)MathF.Floor(cx - radius));
            int maxX = Math.Min(viewW - 1, (int)MathF.Ceiling(cx + radius));
            int minY = Math.Max(0, (int)MathF.Floor(cy - radius));
            int maxY = Math.Min(viewH - 1, (int)MathF.Ceiling(cy + radius));
            float invR = 1f / radius;

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * viewW;
                float dy = (y + 0.5f - cy) * invR;
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x + 0.5f - cx) * invR;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) continue;
                    float falloff = (1f - d) * (1f - d);
                    float a = falloff * intensity * 0.5f;
                    int addR = (int)(colorR * a);
                    int addG = (int)(colorG * a);
                    int addB = (int)(colorB * a);
                    int idx = row + x;
                    pixels[idx] = Additive(pixels[idx], addR, addG, addB);
                }
            }
        }
    }

    private void DrawMouseCrosshair()
    {
        int mx, my;
        _sdl.GetWindowSize(_window, &mx, &my);
        if (mx <= 0 || my <= 0)
            return;

        int mouseX;
        int mouseY;
        _sdl.GetMouseState(&mouseX, &mouseY);
        if (mouseX < 0 || mouseY < 0 || mouseX >= mx || mouseY >= my)
            return;

        const int arm = 10;
        const int gap = 3;
        _sdl.SetRenderDrawColor(_renderer, 255, 255, 255, 255);
        _sdl.RenderDrawLine(_renderer, mouseX - arm, mouseY, mouseX - gap, mouseY);
        _sdl.RenderDrawLine(_renderer, mouseX + gap, mouseY, mouseX + arm, mouseY);
        _sdl.RenderDrawLine(_renderer, mouseX, mouseY - arm, mouseX, mouseY - gap);
        _sdl.RenderDrawLine(_renderer, mouseX, mouseY + gap, mouseX, mouseY + arm);
    }

    private static uint Additive(uint color, int addR, int addG, int addB)
    {
        int r = Math.Min(255, (int)((color >> 16) & 0xFF) + addR);
        int g = Math.Min(255, (int)((color >> 8) & 0xFF) + addG);
        int b = Math.Min(255, (int)(color & 0xFF) + addB);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }
}
