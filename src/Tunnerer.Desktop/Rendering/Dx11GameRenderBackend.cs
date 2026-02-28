namespace Tunnerer.Desktop.Rendering;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.SDL;
using Tunnerer.Core.Config;
using Tunnerer.Core.Types;
using System.Runtime.InteropServices;
using System.Text;

public sealed unsafe class Dx11GameRenderBackend : IGameRenderBackend
{
    private readonly Sdl _sdl;
    private readonly Window* _window;
    private readonly D3D11 _d3d11;

    // Native DX11 pipeline
    private ID3D11Device* _device;
    private ID3D11DeviceContext* _context;
    private IDXGISwapChain* _swapChain;
    private ID3D11RenderTargetView* _backbufferRTV;
    private ID3D11Texture2D* _backbufferTex;
    private ID3D11Texture2D* _gameFrameTex;
    private ID3D11ShaderResourceView* _gameFrameSrv;
    private ID3D11VertexShader* _fullscreenVs;
    private ID3D11PixelShader* _fullscreenPs;
    private ID3D11SamplerState* _fullscreenSampler;
    private int _gameFrameTexW, _gameFrameTexH;
    private int _swapChainW, _swapChainH;
    private bool _nativeReady;

    // SDL fallback pipeline
    private Renderer* _sdlRenderer;
    private Texture* _sdlFrameTexture;
    private int _sdlFrameW, _sdlFrameH;

    private uint[] _processedPixels = Array.Empty<uint>();
    private bool _disposed;

    public nint GameTextureId => nint.Zero;
    public bool SupportsUi => false;

    public Dx11GameRenderBackend(Sdl sdl, Window* window)
    {
        _sdl = sdl;
        _window = window;
#pragma warning disable CS0618
        _d3d11 = D3D11.GetApi();
#pragma warning restore CS0618

        if (!TryInitNativeSwapChain())
            InitSdlFallback();
    }

    public void ProcessEvent(Event ev) { _ = ev; }

    public void UploadGamePixels(in GamePixelsUpload upload)
    {
        int w = upload.View.ViewSize.X;
        int h = upload.View.ViewSize.Y;
        EnsureProcessedPixels(w * h);
        Array.Copy(upload.Pixels, _processedPixels, _processedPixels.Length);
        ApplyFallbackEffects(_processedPixels, upload);
        DrawCrosshairIntoPixels(_processedPixels, w, h);

        if (_nativeReady)
            UploadNative(w, h);
        else
            UploadSdl(w, h);
    }

    public void ClearFrame(Size viewportSize, Tunnerer.Core.Types.Color c)
    {
        if (_nativeReady)
        {
            float* color = stackalloc float[4];
            color[0] = c.R / 255f;
            color[1] = c.G / 255f;
            color[2] = c.B / 255f;
            color[3] = c.A / 255f;
            _context->ClearRenderTargetView(_backbufferRTV, color);
        }
        else
        {
            _ = viewportSize;
            _sdl.SetRenderDrawColor(_sdlRenderer, c.R, c.G, c.B, c.A);
            _sdl.RenderClear(_sdlRenderer);
        }
    }

    public void NewFrame(int windowW, int windowH, float deltaTime)
    {
        _ = windowW; _ = windowH; _ = deltaTime;
    }

    public void Render()
    {
        if (_nativeReady)
        {
            if (!EnsureSwapChainSize())
                return;

            if (_gameFrameSrv != null)
            {
                ID3D11RenderTargetView* rtv = _backbufferRTV;
                _context->OMSetRenderTargets(1, &rtv, null);
                var viewport = new Viewport(0, 0, (uint)_swapChainW, (uint)_swapChainH, 0f, 1f);
                _context->RSSetViewports(1, &viewport);
                _context->IASetInputLayout(null);
                _context->IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
                _context->VSSetShader(_fullscreenVs, null, 0);
                _context->PSSetShader(_fullscreenPs, null, 0);
                ID3D11SamplerState* sampler = _fullscreenSampler;
                _context->PSSetSamplers(0, 1, &sampler);
                ID3D11ShaderResourceView* srv = _gameFrameSrv;
                _context->PSSetShaderResources(0, 1, &srv);
                _context->Draw(3, 0);
            }
            _swapChain->Present(1, 0);
        }
        else
        {
            if (_sdlFrameTexture != null)
                _sdl.RenderCopy(_sdlRenderer, _sdlFrameTexture, null, null);
            _sdl.RenderPresent(_sdlRenderer);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_gameFrameTex != null) { _gameFrameTex->Release(); _gameFrameTex = null; }
        if (_gameFrameSrv != null) { _gameFrameSrv->Release(); _gameFrameSrv = null; }
        if (_fullscreenVs != null) { _fullscreenVs->Release(); _fullscreenVs = null; }
        if (_fullscreenPs != null) { _fullscreenPs->Release(); _fullscreenPs = null; }
        if (_fullscreenSampler != null) { _fullscreenSampler->Release(); _fullscreenSampler = null; }
        if (_backbufferRTV != null) { _backbufferRTV->Release(); _backbufferRTV = null; }
        if (_backbufferTex != null) { _backbufferTex->Release(); _backbufferTex = null; }
        if (_swapChain != null) { _swapChain->Release(); _swapChain = null; }
        if (_context != null) { _context->Release(); _context = null; }
        if (_device != null) { _device->Release(); _device = null; }

        if (_sdlFrameTexture != null) { _sdl.DestroyTexture(_sdlFrameTexture); _sdlFrameTexture = null; }
        if (_sdlRenderer != null) { _sdl.DestroyRenderer(_sdlRenderer); _sdlRenderer = null; }

        _d3d11.Dispose();
    }

    // ── Native DX11 init ──────────────────────────────────────────────

    private bool TryInitNativeSwapChain()
    {
        nint hwnd = GetHwnd();
        if (hwnd == nint.Zero)
        {
            Console.WriteLine("[Render] DX11 native init skipped: could not extract HWND.");
            return false;
        }

        int winW, winH;
        _sdl.GetWindowSize(_window, &winW, &winH);
        if (winW <= 0 || winH <= 0) return false;

        var featureLevel = D3DFeatureLevel.Level110;
        var scDesc = new SwapChainDesc
        {
            BufferDesc = new ModeDesc
            {
                Width = (uint)winW,
                Height = (uint)winH,
                Format = Format.FormatB8G8R8A8Unorm,
                RefreshRate = new Rational(60, 1),
            },
            SampleDesc = new SampleDesc(1, 0),
            BufferUsage = 0x20u, // DXGI_USAGE_RENDER_TARGET_OUTPUT
            BufferCount = 2,
            OutputWindow = hwnd,
            Windowed = true,
            SwapEffect = SwapEffect.Discard,
            Flags = 0,
        };

        ID3D11Device* dev = null;
        ID3D11DeviceContext* ctx = null;
        IDXGISwapChain* sc = null;
        D3DFeatureLevel created = default;

        int hr = _d3d11.CreateDeviceAndSwapChain(
            (IDXGIAdapter*)null,
            D3DDriverType.Hardware,
            0, 0u,
            &featureLevel, 1u,
            D3D11.SdkVersion,
            &scDesc, &sc,
            &dev, &created, &ctx);

        if (hr < 0 || dev == null || ctx == null || sc == null)
        {
            Console.WriteLine($"[Render] DX11 CreateDeviceAndSwapChain failed (hr=0x{hr:X8}).");
            if (sc != null) sc->Release();
            if (ctx != null) ctx->Release();
            if (dev != null) dev->Release();
            return false;
        }

        _device = dev;
        _context = ctx;
        _swapChain = sc;

        if (!AcquireBackbufferRTV())
        {
            _swapChain->Release(); _swapChain = null;
            _context->Release(); _context = null;
            _device->Release(); _device = null;
            return false;
        }

        if (!CreateFullscreenPipeline())
        {
            _backbufferRTV->Release(); _backbufferRTV = null;
            _backbufferTex->Release(); _backbufferTex = null;
            _swapChain->Release(); _swapChain = null;
            _context->Release(); _context = null;
            _device->Release(); _device = null;
            return false;
        }

        _swapChainW = winW;
        _swapChainH = winH;
        _nativeReady = true;
        Console.WriteLine($"[Render] DX11 native pipeline ready ({winW}x{winH}, feature={created}).");
        return true;
    }

    private bool AcquireBackbufferRTV()
    {
        Guid tex2dGuid = ID3D11Texture2D.Guid;
        ID3D11Texture2D* bb = null;
        int hr = _swapChain->GetBuffer(0, &tex2dGuid, (void**)&bb);
        if (hr < 0 || bb == null) return false;
        _backbufferTex = bb;

        ID3D11RenderTargetView* rtv = null;
        hr = _device->CreateRenderTargetView((ID3D11Resource*)bb, null, &rtv);
        if (hr < 0 || rtv == null) { bb->Release(); _backbufferTex = null; return false; }
        _backbufferRTV = rtv;
        return true;
    }

    private bool CreateFullscreenPipeline()
    {
        const string vsSource = @"
struct VSOut
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

VSOut main(uint id : SV_VertexID)
{
    VSOut o;
    if (id == 0) { o.pos = float4(-1, -1, 0, 1); o.uv = float2(0, 1); }
    else if (id == 1) { o.pos = float4(-1, 3, 0, 1); o.uv = float2(0, -1); }
    else { o.pos = float4(3, -1, 0, 1); o.uv = float2(2, 1); }
    return o;
}";

        const string psSource = @"
Texture2D t0 : register(t0);
SamplerState s0 : register(s0);

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    return t0.Sample(s0, uv);
}";

        ID3DBlob* vsBlob;
        ID3DBlob* psBlob;
        try
        {
            vsBlob = CompileShader(vsSource, "main", "vs_5_0");
            if (vsBlob == null)
                return false;
            psBlob = CompileShader(psSource, "main", "ps_5_0");
        }
        catch (DllNotFoundException e)
        {
            Console.WriteLine($"[Render] D3DCompile unavailable: {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Render] D3DCompile failed: {e.Message}");
            return false;
        }
        if (psBlob == null)
        {
            BlobRelease(vsBlob);
            return false;
        }

        ID3D11VertexShader* vs = null;
        int hr = _device->CreateVertexShader(BlobGetBufferPointer(vsBlob), BlobGetBufferSize(vsBlob), null, &vs);
        if (hr < 0 || vs == null)
        {
            BlobRelease(vsBlob);
            BlobRelease(psBlob);
            return false;
        }
        _fullscreenVs = vs;
        ID3D11PixelShader* ps = null;
        hr = _device->CreatePixelShader(BlobGetBufferPointer(psBlob), BlobGetBufferSize(psBlob), null, &ps);
        BlobRelease(vsBlob);
        BlobRelease(psBlob);
        if (hr < 0 || ps == null)
            return false;
        _fullscreenPs = ps;

        var sampDesc = new SamplerDesc
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            ComparisonFunc = ComparisonFunc.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue,
        };
        ID3D11SamplerState* sampler = null;
        hr = _device->CreateSamplerState(&sampDesc, &sampler);
        if (hr < 0 || sampler == null)
            return false;
        _fullscreenSampler = sampler;

        return true;
    }

    private bool EnsureSwapChainSize()
    {
        int winW, winH;
        _sdl.GetWindowSize(_window, &winW, &winH);
        if (winW <= 0 || winH <= 0)
            return false;
        if (winW == _swapChainW && winH == _swapChainH)
            return true;

        if (_backbufferRTV != null) { _backbufferRTV->Release(); _backbufferRTV = null; }
        if (_backbufferTex != null) { _backbufferTex->Release(); _backbufferTex = null; }

        int hr = _swapChain->ResizeBuffers(0, (uint)winW, (uint)winH, Format.FormatUnknown, 0);
        if (hr < 0)
        {
            Console.WriteLine($"[Render] DX11 ResizeBuffers failed (hr=0x{hr:X8}).");
            return false;
        }
        if (!AcquireBackbufferRTV())
            return false;

        _swapChainW = winW;
        _swapChainH = winH;
        return true;
    }

    private nint GetHwnd()
    {
        SysWMInfo wmInfo = default;
        _sdl.GetVersion(&wmInfo.Version);
        if (!_sdl.GetWindowWMInfo(_window, &wmInfo))
            return nint.Zero;
        if (wmInfo.Subsystem != SysWMType.Windows)
            return nint.Zero;
        return wmInfo.Info.Win.Hwnd;
    }

    // ── Native upload path ────────────────────────────────────────────

    private void UploadNative(int w, int h)
    {
        EnsureNativeGameTexture(w, h);
        fixed (uint* ptr = _processedPixels)
        {
            _context->UpdateSubresource(
                (ID3D11Resource*)_gameFrameTex, 0, null, ptr, (uint)(w * sizeof(uint)), 0);
        }
    }

    private void EnsureNativeGameTexture(int w, int h)
    {
        if (_gameFrameTex != null && _gameFrameTexW == w && _gameFrameTexH == h)
            return;
        if (_gameFrameTex != null) { _gameFrameTex->Release(); _gameFrameTex = null; }

        var desc = new Texture2DDesc
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.ShaderResource,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ID3D11Texture2D* tex = null;
        _device->CreateTexture2D(&desc, null, &tex);
        _gameFrameTex = tex;
        if (_gameFrameSrv != null) { _gameFrameSrv->Release(); _gameFrameSrv = null; }
        ID3D11ShaderResourceView* srv = null;
        _device->CreateShaderResourceView((ID3D11Resource*)tex, null, &srv);
        _gameFrameSrv = srv;
        _gameFrameTexW = w;
        _gameFrameTexH = h;
    }

    // ── SDL fallback init ─────────────────────────────────────────────

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

    // ── Pixel post-processing (CPU, shared by both paths) ─────────────

    private void EnsureProcessedPixels(int count)
    {
        if (_processedPixels.Length != count)
            _processedPixels = new uint[count];
    }

    private static void ApplyFallbackEffects(uint[] pixels, in GamePixelsUpload upload)
    {
        ApplyTerrainAuxLighting(pixels, upload);
        ApplyTankHeatGlow(pixels, upload);
    }

    private static void ApplyTerrainAuxLighting(uint[] pixels, in GamePixelsUpload upload)
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
        float pulseTime = (Environment.TickCount64 & 0x7FFFFFFF) * 0.001f;
        float pulse = Tweaks.Screen.PostMaterialEmissivePulseMin +
                      Tweaks.Screen.PostMaterialEmissivePulseRange *
                      (0.5f + 0.5f * MathF.Sin(pulseTime * Tweaks.Screen.PostMaterialEmissivePulseFreq));
        int energyR = (int)(Tweaks.Screen.PostMaterialEmissiveEnergyR * 255f);
        int energyG = (int)(Tweaks.Screen.PostMaterialEmissiveEnergyG * 255f);
        int energyB = (int)(Tweaks.Screen.PostMaterialEmissiveEnergyB * 255f);
        int scorchedR = (int)(Tweaks.Screen.PostMaterialEmissiveScorchedR * 255f);
        int scorchedG = (int)(Tweaks.Screen.PostMaterialEmissiveScorchedG * 255f);
        int scorchedB = (int)(Tweaks.Screen.PostMaterialEmissiveScorchedB * 255f);

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
                byte energyByte = aux[auxIdx + 2];
                byte scorchedByte = aux[auxIdx + 3];

                int addR = 0, addG = 0, addB = 0;

                if (heat > threshold)
                {
                    float t = (heat - threshold) / MathF.Max(0.0001f, 1f - threshold);
                    addR += (int)(glowR * t * 0.35f);
                    addG += (int)(glowG * t * 0.35f);
                    addB += (int)(glowB * t * 0.35f);
                }
                if (energyByte > 0)
                {
                    float t = (energyByte / 255f) * Tweaks.Screen.PostMaterialEmissiveEnergyStrength * pulse;
                    addR += (int)(energyR * t);
                    addG += (int)(energyG * t);
                    addB += (int)(energyB * t);
                }
                if (scorchedByte > 0)
                {
                    float t = (scorchedByte / 255f) * Tweaks.Screen.PostMaterialEmissiveScorchedStrength * pulse;
                    addR += (int)(scorchedR * t);
                    addG += (int)(scorchedG * t);
                    addB += (int)(scorchedB * t);
                }
                if (addR != 0 || addG != 0 || addB != 0)
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
                    int idx = row + x;
                    pixels[idx] = Additive(pixels[idx], (int)(colorR * a), (int)(colorG * a), (int)(colorB * a));
                }
            }
        }
    }

    private void DrawCrosshairIntoPixels(uint[] pixels, int w, int h)
    {
        int mx, my;
        _sdl.GetMouseState(&mx, &my);
        int winW, winH;
        _sdl.GetWindowSize(_window, &winW, &winH);
        if (winW <= 0 || winH <= 0 || mx < 0 || my < 0 || mx >= winW || my >= winH)
            return;

        int px = mx * w / winW;
        int py = my * h / winH;

        const int arm = 10;
        const int gap = 3;
        const uint col = 0xFFFFFFFF;
        for (int i = gap; i <= arm; i++)
        {
            SetPixel(pixels, w, h, px - i, py, col);
            SetPixel(pixels, w, h, px + i, py, col);
            SetPixel(pixels, w, h, px, py - i, col);
            SetPixel(pixels, w, h, px, py + i, col);
        }
    }

    private static void SetPixel(uint[] buf, int w, int h, int x, int y, uint color)
    {
        if ((uint)x < (uint)w && (uint)y < (uint)h)
            buf[y * w + x] = color;
    }

    private static uint Additive(uint color, int addR, int addG, int addB)
    {
        int r = Math.Min(255, (int)((color >> 16) & 0xFF) + addR);
        int g = Math.Min(255, (int)((color >> 8) & 0xFF) + addG);
        int b = Math.Min(255, (int)(color & 0xFF) + addB);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    // ── D3DCompile helpers ────────────────────────────────────────────

    private ID3DBlob* CompileShader(string source, string entryPoint, string target)
    {
        byte[] srcBytes = Encoding.UTF8.GetBytes(source);
        byte[] entryBytes = Encoding.ASCII.GetBytes(entryPoint + "\0");
        byte[] targetBytes = Encoding.ASCII.GetBytes(target + "\0");
        fixed (byte* pSrc = srcBytes)
        fixed (byte* pEntry = entryBytes)
        fixed (byte* pTarget = targetBytes)
        {
            const uint D3DCOMPILE_ENABLE_STRICTNESS = 0x800;
            const uint D3DCOMPILE_OPTIMIZATION_LEVEL3 = 0x4000;
            uint flags = D3DCOMPILE_ENABLE_STRICTNESS | D3DCOMPILE_OPTIMIZATION_LEVEL3;
            ID3DBlob* code = null;
            ID3DBlob* errors = null;
            int hr = D3DCompile(
                pSrc, (nuint)srcBytes.Length,
                null, null, null,
                (sbyte*)pEntry, (sbyte*)pTarget,
                flags, 0,
                &code, &errors);
            if (hr < 0 || code == null)
            {
                if (errors != null)
                {
                    string msg = Marshal.PtrToStringAnsi((nint)BlobGetBufferPointer(errors)) ?? "unknown";
                    Console.WriteLine($"[Render] D3DCompile failed: {msg}");
                    BlobRelease(errors);
                }
                return null;
            }
            if (errors != null)
                BlobRelease(errors);
            return code;
        }
    }

    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int D3DCompile(
        byte* pSrcData,
        nuint srcDataSize,
        sbyte* pSourceName,
        void* pDefines,
        void* pInclude,
        sbyte* pEntryPoint,
        sbyte* pTarget,
        uint flags1,
        uint flags2,
        ID3DBlob** ppCode,
        ID3DBlob** ppErrorMsgs);

    private static void* BlobGetBufferPointer(ID3DBlob* blob)
    {
        var fn = (delegate* unmanaged[Stdcall]<ID3DBlob*, void*>)(blob->LpVtbl[3]);
        return fn(blob);
    }

    private static nuint BlobGetBufferSize(ID3DBlob* blob)
    {
        var fn = (delegate* unmanaged[Stdcall]<ID3DBlob*, nuint>)(blob->LpVtbl[4]);
        return fn(blob);
    }

    private static void BlobRelease(ID3DBlob* blob)
    {
        var fn = (delegate* unmanaged[Stdcall]<ID3DBlob*, uint>)(blob->LpVtbl[2]);
        _ = fn(blob);
    }

    private struct ID3DBlob
    {
        public void** LpVtbl;
    }
}
