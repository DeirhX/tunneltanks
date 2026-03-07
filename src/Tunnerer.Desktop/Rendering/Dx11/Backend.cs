namespace Tunnerer.Desktop.Rendering.Dx11;

using ImGuiNET;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.SDL;
using Tunnerer.Core.Config;
using Tunnerer.Core.Types;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Tunnerer.Desktop.Rendering;

public sealed unsafe partial class Backend : IGameRenderBackend
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
    private ID3D11Texture2D* _sceneTexture;
    private ID3D11ShaderResourceView* _sceneSrv;
    private ID3D11RenderTargetView* _sceneRtv;
    private ID3D11Texture2D* _postTexture;
    private ID3D11ShaderResourceView* _postSrv;
    private ID3D11RenderTargetView* _postRtv;
    private ID3D11Texture2D* _nativeSourceTexture;
    private ID3D11ShaderResourceView* _nativeSourceSrv;
    private int _nativeSourceW, _nativeSourceH;
    private ID3D11Texture2D* _terrainAuxTexture;
    private ID3D11ShaderResourceView* _terrainAuxSrv;
    private byte[]? _terrainAuxUploadScratch;
    private ID3D11VertexShader* _fullscreenVs;
    private ID3D11PixelShader* _blitPs;
    private ID3D11PixelShader* _postPs;
    private ID3D11PixelShader* _terrainPs;
    private ID3D11SamplerState* _fullscreenSampler;
    private ID3D11Buffer* _postParamsBuffer;
    private ID3D11ShaderResourceView* _displaySrv;
    private ImGuiController? _imgui;
    private readonly bool _detailedProfileEnabled =
        string.Equals(Environment.GetEnvironmentVariable("TUNNERER_DX11_PROFILE"), "1", StringComparison.Ordinal);
    private readonly bool _debugDumpFrames =
        string.Equals(Environment.GetEnvironmentVariable("TUNNERER_DX11_DUMP_FRAMES"), "1", StringComparison.Ordinal);
    private bool _debugDumpDone;
    private long _profileSceneUploadTicks;
    private long _profileAuxUploadTicks;
    private long _profilePostPassTicks;
    private long _profileNativeTerrainPassTicks;
    private long _profilePostSetupTicks;
    private long _profilePostCbUpdateTicks;
    private long _profilePostDrawTicks;
    private long _profileFinalBlitTicks;
    private long _profileUiRenderTicks;
    private int _profileFrameCount;
    private int _sceneTexW, _sceneTexH;
    private int _terrainAuxW, _terrainAuxH;
    private int _swapChainW, _swapChainH;
    private string? _pendingScreenshotPath;
    private bool _disposed;

    internal ID3D11Device* Device => _device;
    public nint GameTextureId => (nint)(_displaySrv != null ? _displaySrv : _sceneSrv);
    public Size GameTextureSize => new(Math.Max(1, _sceneTexW), Math.Max(1, _sceneTexH));
    public bool SupportsUi => _imgui != null;

    public Backend(Sdl sdl, Window* window)
    {
        _sdl = sdl;
        _window = window;
#pragma warning disable CS0618
        _d3d11 = D3D11.GetApi();
#pragma warning restore CS0618

        if (!TryInitNativeSwapChain())
            throw new InvalidOperationException(
                "DX11 native initialization failed. DX11 CPU/SDL fallback has been removed. " +
                "Use a DX11-capable GPU/driver or switch render backend.");
    }

    public void ProcessEvent(Event ev)
    {
        if (SupportsUi)
            _imgui!.ProcessEvent(ev);
    }

    public void UploadGamePixels(in GamePixelsUpload upload)
    {
        int w = upload.View.ViewSize.X;
        int h = upload.View.ViewSize.Y;
        UploadNative(w, h, upload);
    }

    public void ClearFrame(Size viewportSize, Tunnerer.Core.Types.Color c)
    {
        _ = viewportSize;
        float* color = stackalloc float[4];
        color[0] = c.R / 255f;
        color[1] = c.G / 255f;
        color[2] = c.B / 255f;
        color[3] = c.A / 255f;
        _context->ClearRenderTargetView(_backbufferRTV, color);
    }

    public void NewFrame(int windowW, int windowH, float deltaTime)
    {
        if (SupportsUi)
            _imgui!.NewFrame(windowW, windowH, deltaTime);
    }

    public void Render()
    {
        if (!EnsureSwapChainSize())
            return;

        if (_displaySrv != null || _sceneSrv != null)
        {
            long t0 = Stopwatch.GetTimestamp();
            // Match blit viewport to uploaded scene size to avoid stretching
            // into the bottom HUD strip area.
            int blitW = _sceneTexW > 0 ? Math.Min(_sceneTexW, _swapChainW) : _swapChainW;
            int blitH = _sceneTexH > 0 ? Math.Min(_sceneTexH, _swapChainH) : _swapChainH;
            PrepareFullscreenPass(_backbufferRTV, blitW, blitH, _blitPs);
            ID3D11ShaderResourceView* srv = _displaySrv != null ? _displaySrv : _sceneSrv;
            _context->PSSetShaderResources(0, 1, &srv);
            _context->Draw(3, 0);
            ID3D11ShaderResourceView* clearSrv = null;
            _context->PSSetShaderResources(0, 1, &clearSrv);
            _profileFinalBlitTicks += Stopwatch.GetTimestamp() - t0;
        }

        if (SupportsUi)
        {
            long t0 = Stopwatch.GetTimestamp();
            _imgui!.Render(_backbufferRTV, _swapChainW, _swapChainH);
            _profileUiRenderTicks += Stopwatch.GetTimestamp() - t0;
        }

        // Capture screenshot from final composited backbuffer (includes UI/HUD).
        TryCapturePendingFinalScreenshot();
        _swapChain->Present(1, 0);
    }

    public void RequestScreenshot(string? label = null)
    {
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        string cleanLabel = string.IsNullOrWhiteSpace(label) ? "frame" : SanitizeFilePart(label);
        string dir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(dir);
        _pendingScreenshotPath = Path.Combine(dir, $"{stamp}_{cleanLabel}.ppm");
        Console.WriteLine($"[Render] Queued final-frame screenshot capture: {_pendingScreenshotPath}");
    }

    private static string SanitizeFilePart(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        Span<char> buffer = stackalloc char[value.Length];
        int j = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool bad = false;
            for (int k = 0; k < invalid.Length; k++)
            {
                if (c == invalid[k])
                {
                    bad = true;
                    break;
                }
            }

            buffer[j++] = bad || char.IsWhiteSpace(c) ? '_' : c;
        }
        return new string(buffer[..j]);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_detailedProfileEnabled && _profileFrameCount > 0)
            FlushDetailedProfile();

        _imgui?.Dispose();
        _imgui = null;

        if (_sceneTexture != null) { _sceneTexture->Release(); _sceneTexture = null; }
        if (_sceneSrv != null) { _sceneSrv->Release(); _sceneSrv = null; }
        if (_sceneRtv != null) { _sceneRtv->Release(); _sceneRtv = null; }
        if (_postTexture != null) { _postTexture->Release(); _postTexture = null; }
        if (_postSrv != null) { _postSrv->Release(); _postSrv = null; }
        if (_postRtv != null) { _postRtv->Release(); _postRtv = null; }
        if (_nativeSourceTexture != null) { _nativeSourceTexture->Release(); _nativeSourceTexture = null; }
        if (_nativeSourceSrv != null) { _nativeSourceSrv->Release(); _nativeSourceSrv = null; }
        if (_terrainAuxTexture != null) { _terrainAuxTexture->Release(); _terrainAuxTexture = null; }
        if (_terrainAuxSrv != null) { _terrainAuxSrv->Release(); _terrainAuxSrv = null; }
        if (_fullscreenVs != null) { _fullscreenVs->Release(); _fullscreenVs = null; }
        if (_blitPs != null) { _blitPs->Release(); _blitPs = null; }
        if (_postPs != null) { _postPs->Release(); _postPs = null; }
        if (_terrainPs != null) { _terrainPs->Release(); _terrainPs = null; }
        if (_fullscreenSampler != null) { _fullscreenSampler->Release(); _fullscreenSampler = null; }
        if (_postParamsBuffer != null) { _postParamsBuffer->Release(); _postParamsBuffer = null; }
        if (_backbufferRTV != null) { _backbufferRTV->Release(); _backbufferRTV = null; }
        if (_backbufferTex != null) { _backbufferTex->Release(); _backbufferTex = null; }
        if (_swapChain != null) { _swapChain->Release(); _swapChain = null; }
        if (_context != null) { _context->Release(); _context = null; }
        if (_device != null) { _device->Release(); _device = null; }

        _d3d11.Dispose();
    }

}
