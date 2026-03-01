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
    private ID3D11VertexShader* _fullscreenVs;
    private ID3D11PixelShader* _blitPs;
    private ID3D11PixelShader* _postPs;
    private ID3D11PixelShader* _terrainPs;
    private ID3D11SamplerState* _fullscreenSampler;
    private ID3D11Buffer* _postParamsBuffer;
    private ID3D11ShaderResourceView* _displaySrv;
    private ImGuiController? _imgui;
    private readonly bool _forceCpuFallbackEffects =
        string.Equals(Environment.GetEnvironmentVariable("TUNNERER_DX11_CPU_FALLBACK"), "1", StringComparison.Ordinal);
    private readonly bool _detailedProfileEnabled =
        string.Equals(Environment.GetEnvironmentVariable("TUNNERER_DX11_PROFILE"), "1", StringComparison.Ordinal);
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
    private bool _nativeReady;

    // SDL fallback pipeline
    private Renderer* _sdlRenderer;
    private Texture* _sdlFrameTexture;
    private int _sdlFrameW, _sdlFrameH;

    private uint[] _processedPixels = Array.Empty<uint>();
    private bool _disposed;

    internal ID3D11Device* Device => _device;
    internal bool IsNativeReady => _nativeReady;
    public nint GameTextureId => (nint)(_displaySrv != null ? _displaySrv : _sceneSrv);
    public bool SupportsUi => _nativeReady && _imgui != null;

    public Backend(Sdl sdl, Window* window)
    {
        _sdl = sdl;
        _window = window;
#pragma warning disable CS0618
        _d3d11 = D3D11.GetApi();
#pragma warning restore CS0618

        if (!TryInitNativeSwapChain())
            InitSdlFallback();
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

        if (_nativeReady)
            UploadNative(w, h, upload);
        else
        {
            EnsureProcessedPixels(w * h);
            Array.Copy(upload.Pixels, _processedPixels, _processedPixels.Length);
            ApplyFallbackEffects(_processedPixels, upload);
            DrawCrosshairIntoPixels(_processedPixels, w, h);
            UploadSdl(w, h);
        }
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
        if (SupportsUi)
            _imgui!.NewFrame(windowW, windowH, deltaTime);
    }

    public void Render()
    {
        if (_nativeReady)
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

        if (_sdlFrameTexture != null) { _sdl.DestroyTexture(_sdlFrameTexture); _sdlFrameTexture = null; }
        if (_sdlRenderer != null) { _sdl.DestroyRenderer(_sdlRenderer); _sdlRenderer = null; }

        _d3d11.Dispose();
    }

}
