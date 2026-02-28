namespace Tunnerer.Desktop.Rendering;

using ImGuiNET;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.SDL;
using Tunnerer.Core.Config;
using Tunnerer.Core.Types;
using System.Diagnostics;
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
    private ID3D11Texture2D* _sceneTexture;
    private ID3D11ShaderResourceView* _sceneSrv;
    private ID3D11Texture2D* _postTexture;
    private ID3D11ShaderResourceView* _postSrv;
    private ID3D11RenderTargetView* _postRtv;
    private ID3D11Texture2D* _terrainAuxTexture;
    private ID3D11ShaderResourceView* _terrainAuxSrv;
    private ID3D11VertexShader* _fullscreenVs;
    private ID3D11PixelShader* _blitPs;
    private ID3D11PixelShader* _postPs;
    private ID3D11SamplerState* _fullscreenSampler;
    private ID3D11Buffer* _postParamsBuffer;
    private ID3D11ShaderResourceView* _displaySrv;
    private Dx11ImGuiController? _imgui;
    private readonly bool _forceCpuFallbackEffects =
        string.Equals(Environment.GetEnvironmentVariable("TUNNERER_DX11_CPU_FALLBACK"), "1", StringComparison.Ordinal);
    private readonly bool _detailedProfileEnabled =
        string.Equals(Environment.GetEnvironmentVariable("TUNNERER_DX11_PROFILE"), "1", StringComparison.Ordinal);
    private long _profileSceneUploadTicks;
    private long _profileAuxUploadTicks;
    private long _profilePostPassTicks;
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
                PrepareFullscreenPass(_backbufferRTV, _swapChainW, _swapChainH, _blitPs);
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
        if (_postTexture != null) { _postTexture->Release(); _postTexture = null; }
        if (_postSrv != null) { _postSrv->Release(); _postSrv = null; }
        if (_postRtv != null) { _postRtv->Release(); _postRtv = null; }
        if (_terrainAuxTexture != null) { _terrainAuxTexture->Release(); _terrainAuxTexture = null; }
        if (_terrainAuxSrv != null) { _terrainAuxSrv->Release(); _terrainAuxSrv = null; }
        if (_fullscreenVs != null) { _fullscreenVs->Release(); _fullscreenVs = null; }
        if (_blitPs != null) { _blitPs->Release(); _blitPs = null; }
        if (_postPs != null) { _postPs->Release(); _postPs = null; }
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
        _imgui = new Dx11ImGuiController(_sdl, _window, _device, _context, winW, winH);
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

        const string blitPsSource = @"
Texture2D t0 : register(t0);
SamplerState s0 : register(s0);

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    return t0.Sample(s0, uv);
}";

        const string postPsSource = @"
Texture2D sceneTex : register(t0);
Texture2D auxTex : register(t1);
SamplerState s0 : register(s0);

cbuffer PostParams : register(b0)
{
    float2 TexelSize;
    float PixelScale;
    float Time;
    float2 WorldSize;
    float2 CameraPixels;
    float2 ViewSize;
    float UseTerrainAux;
    float BloomThreshold;
    float BloomStrength;
    float BloomWeightCenter;
    float BloomWeightAxis;
    float BloomWeightDiagonal;
    float VignetteStrength;
    float EdgeLightStrength;
    float EdgeLightBias;
    float4 TankHeatGlowColor;
    float4 TerrainHeatGlowColorAndThreshold;
    float TerrainMaskEdgeStrength;
    float TerrainMaskCaveDarken;
    float TerrainMaskSolidLift;
    float TerrainMaskOutlineDarken;
    float TerrainMaskRimLift;
    float TerrainMaskBoundaryScale;
    float VignetteInnerRadius;
    float VignetteOuterRadius;
    float Quality;
    float4 MaterialEmissiveEnergy;
    float4 MaterialEmissiveScorched;
    float4 MaterialEmissivePulse;
    float TankGlowCount;
    float4 TankGlow[8];
};

float3 bright(float3 c) { return max(c - float3(BloomThreshold, BloomThreshold, BloomThreshold), 0.0); }

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    float3 baseColor = sceneTex.Sample(s0, uv).rgb;
    float3 color = baseColor;

    if (Quality >= 1.0)
    {
        float2 tx = float2(TexelSize.x, 0.0);
        float2 ty = float2(0.0, TexelSize.y);
        float2 d1 = float2(TexelSize.x, TexelSize.y);
        float2 d2 = float2(TexelSize.x, -TexelSize.y);
        float3 bloom = bright(baseColor) * BloomWeightCenter;
        bloom += bright(sceneTex.Sample(s0, uv + tx).rgb) * BloomWeightAxis;
        bloom += bright(sceneTex.Sample(s0, uv - tx).rgb) * BloomWeightAxis;
        bloom += bright(sceneTex.Sample(s0, uv + ty).rgb) * BloomWeightAxis;
        bloom += bright(sceneTex.Sample(s0, uv - ty).rgb) * BloomWeightAxis;
        bloom += bright(sceneTex.Sample(s0, uv + d1).rgb) * BloomWeightDiagonal;
        bloom += bright(sceneTex.Sample(s0, uv - d1).rgb) * BloomWeightDiagonal;
        bloom += bright(sceneTex.Sample(s0, uv + d2).rgb) * BloomWeightDiagonal;
        bloom += bright(sceneTex.Sample(s0, uv - d2).rgb) * BloomWeightDiagonal;
        color += bloom * BloomStrength;
    }

    if (Quality >= 2.0)
    {
        float d = distance(uv, float2(0.5, 0.5));
        float vig = 1.0 - smoothstep(VignetteInnerRadius, VignetteOuterRadius, d) * VignetteStrength;
        color *= vig;
    }

    if (Quality >= 1.0)
    {
        float l = dot(sceneTex.Sample(s0, uv + float2(-TexelSize.x, 0.0)).rgb, float3(0.299, 0.587, 0.114));
        float r = dot(sceneTex.Sample(s0, uv + float2(TexelSize.x, 0.0)).rgb, float3(0.299, 0.587, 0.114));
        float u = dot(sceneTex.Sample(s0, uv + float2(0.0, -TexelSize.y)).rgb, float3(0.299, 0.587, 0.114));
        float d = dot(sceneTex.Sample(s0, uv + float2(0.0, TexelSize.y)).rgb, float3(0.299, 0.587, 0.114));
        float edge = abs(r - l) + abs(d - u);
        float edgeLift = max(0.0, edge - EdgeLightBias) * EdgeLightStrength;
        color += edgeLift.xxx;
    }

    if (UseTerrainAux > 0.5 && PixelScale > 0.0)
    {
        float2 screenPx = uv * ViewSize;
        float2 worldCell = (CameraPixels + screenPx) / PixelScale;
        float2 auxUv = (worldCell + float2(0.5, 0.5)) / WorldSize;
        float2 mTexel = float2(1.0 / WorldSize.x, 1.0 / WorldSize.y);
        float4 a0 = auxTex.Sample(s0, auxUv);
        float4 ax1 = auxTex.Sample(s0, auxUv + float2(mTexel.x, 0.0));
        float4 ax2 = auxTex.Sample(s0, auxUv - float2(mTexel.x, 0.0));
        float4 ay1 = auxTex.Sample(s0, auxUv + float2(0.0, mTexel.y));
        float4 ay2 = auxTex.Sample(s0, auxUv - float2(0.0, mTexel.y));

        float m0 = a0.g;
        float edge = abs(ax1.g - ax2.g) + abs(ay1.g - ay2.g);
        float edgeAmt = min(1.0, edge * TerrainMaskEdgeStrength);
        float boundary = 1.0 - abs(m0 * 2.0 - 1.0);
        float outline = min(1.0, boundary * TerrainMaskBoundaryScale);
        color *= 1.0 - outline * TerrainMaskOutlineDarken;
        if (m0 < 0.5) color *= 1.0 - edgeAmt * TerrainMaskCaveDarken;
        else
        {
            color += edgeAmt * TerrainMaskSolidLift;
            color += edgeAmt * outline * TerrainMaskRimLift;
        }

        float heat = a0.r * 0.50 + (ax1.r + ax2.r + ay1.r + ay2.r) * 0.125;
        if (heat > TerrainHeatGlowColorAndThreshold.a)
        {
            float t2 = heat * heat;
            color.r += TerrainHeatGlowColorAndThreshold.r * t2;
            color.g += TerrainHeatGlowColorAndThreshold.g * t2 * heat;
            color.b += TerrainHeatGlowColorAndThreshold.b * t2 * t2;
        }

        float phase = frac(sin(dot(floor(worldCell), float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
        float pulse = MaterialEmissivePulse.y + MaterialEmissivePulse.z * (0.5 + 0.5 * sin(Time * MaterialEmissivePulse.x + phase));
        color += MaterialEmissiveEnergy.rgb * (a0.b * MaterialEmissiveEnergy.a * pulse);
        color += MaterialEmissiveScorched.rgb * (a0.a * MaterialEmissiveScorched.a * pulse);
    }

    [loop]
    for (int i = 0; i < 8; i++)
    {
        if (i >= (int)TankGlowCount) break;
        float4 g = TankGlow[i];
        float2 d = uv - g.xy;
        float falloff = 1.0 - dot(d, d) / max(1e-6, g.z * g.z);
        if (falloff > 0.0)
        {
            falloff *= falloff;
            color += TankHeatGlowColor.rgb * (g.w * falloff);
        }
    }

    return float4(color, 1.0);
}";

        ID3DBlob* vsBlob;
        ID3DBlob* psBlob;
        try
        {
            vsBlob = CompileShader(vsSource, "main", "vs_5_0");
            if (vsBlob == null)
                return false;
            psBlob = CompileShader(blitPsSource, "main", "ps_5_0");
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
        _blitPs = ps;

        ID3DBlob* postPsBlob = CompileShader(postPsSource, "main", "ps_5_0");
        if (postPsBlob == null)
            return false;
        ID3D11PixelShader* postPs = null;
        hr = _device->CreatePixelShader(BlobGetBufferPointer(postPsBlob), BlobGetBufferSize(postPsBlob), null, &postPs);
        BlobRelease(postPsBlob);
        if (hr < 0 || postPs == null)
            return false;
        _postPs = postPs;

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

        var cbDesc = new BufferDesc
        {
            ByteWidth = (uint)sizeof(PostParamsCBuffer),
            Usage = Usage.Dynamic,
            BindFlags = (uint)BindFlag.ConstantBuffer,
            CPUAccessFlags = (uint)CpuAccessFlag.Write,
            MiscFlags = 0,
            StructureByteStride = 0,
        };
        ID3D11Buffer* cb = null;
        hr = _device->CreateBuffer(&cbDesc, null, &cb);
        if (hr < 0 || cb == null)
            return false;
        _postParamsBuffer = cb;

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

    private void UploadNative(int w, int h, in GamePixelsUpload upload)
    {
        if (_forceCpuFallbackEffects)
        {
            EnsureProcessedPixels(w * h);
            Array.Copy(upload.Pixels, _processedPixels, _processedPixels.Length);
            ApplyFallbackEffects(_processedPixels, upload);
            DrawCrosshairIntoPixels(_processedPixels, w, h);
            EnsureSceneTexture(w, h);
            fixed (uint* ptr = _processedPixels)
            {
                _context->UpdateSubresource(
                    (ID3D11Resource*)_sceneTexture, 0, null, ptr, (uint)(w * sizeof(uint)), 0);
            }
            _displaySrv = _sceneSrv;
            return;
        }

        EnsureSceneTexture(w, h);
        long t0 = Stopwatch.GetTimestamp();
        fixed (uint* ptr = upload.Pixels)
        {
            _context->UpdateSubresource(
                (ID3D11Resource*)_sceneTexture, 0, null, ptr, (uint)(w * sizeof(uint)), 0);
        }
        _profileSceneUploadTicks += Stopwatch.GetTimestamp() - t0;
        t0 = Stopwatch.GetTimestamp();
        UpdateTerrainAuxTexture(upload.TerrainAux, upload.View.WorldSize, upload.AuxDirtyRect);
        _profileAuxUploadTicks += Stopwatch.GetTimestamp() - t0;
        EnsurePostTarget(w, h);
        t0 = Stopwatch.GetTimestamp();
        RunPostProcessPass(upload);
        _profilePostPassTicks += Stopwatch.GetTimestamp() - t0;
        _profileFrameCount++;
        if (_detailedProfileEnabled && _profileFrameCount >= 240)
        {
            FlushDetailedProfile();
        }
    }

    private void EnsureSceneTexture(int w, int h)
    {
        if (_sceneTexture != null && _sceneTexW == w && _sceneTexH == h)
            return;
        if (_sceneTexture != null) { _sceneTexture->Release(); _sceneTexture = null; }

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
        _sceneTexture = tex;
        if (_sceneSrv != null) { _sceneSrv->Release(); _sceneSrv = null; }
        ID3D11ShaderResourceView* srv = null;
        _device->CreateShaderResourceView((ID3D11Resource*)tex, null, &srv);
        _sceneSrv = srv;
        _displaySrv = _sceneSrv;
        _sceneTexW = w;
        _sceneTexH = h;
    }

    private void EnsurePostTarget(int w, int h)
    {
        if (_postTexture != null && _sceneTexW == w && _sceneTexH == h)
            return;
        if (_postRtv != null) { _postRtv->Release(); _postRtv = null; }
        if (_postSrv != null) { _postSrv->Release(); _postSrv = null; }
        if (_postTexture != null) { _postTexture->Release(); _postTexture = null; }

        var desc = new Texture2DDesc
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)(BindFlag.RenderTarget | BindFlag.ShaderResource),
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ID3D11Texture2D* tex = null;
        if (_device->CreateTexture2D(&desc, null, &tex) < 0 || tex == null)
            return;
        _postTexture = tex;
        ID3D11ShaderResourceView* postSrv = null;
        _device->CreateShaderResourceView((ID3D11Resource*)tex, null, &postSrv);
        _postSrv = postSrv;
        ID3D11RenderTargetView* postRtv = null;
        _device->CreateRenderTargetView((ID3D11Resource*)tex, null, &postRtv);
        _postRtv = postRtv;
    }

    private void UpdateTerrainAuxTexture(byte[]? auxData, Size worldSize, Rect? dirtyRect)
    {
        int worldW = worldSize.X;
        int worldH = worldSize.Y;
        if (auxData == null || worldW <= 0 || worldH <= 0)
            return;

        bool sizeChanged = _terrainAuxTexture == null || _terrainAuxW != worldW || _terrainAuxH != worldH;
        if (sizeChanged)
        {
            if (_terrainAuxSrv != null) { _terrainAuxSrv->Release(); _terrainAuxSrv = null; }
            if (_terrainAuxTexture != null) { _terrainAuxTexture->Release(); _terrainAuxTexture = null; }

            var desc = new Texture2DDesc
            {
                Width = (uint)worldW,
                Height = (uint)worldH,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.FormatR8G8B8A8Unorm,
                SampleDesc = new SampleDesc(1, 0),
                Usage = Usage.Default,
                BindFlags = (uint)BindFlag.ShaderResource,
                CPUAccessFlags = 0,
                MiscFlags = 0,
            };
            fixed (byte* ptr = auxData)
            {
                var sub = new SubresourceData
                {
                    PSysMem = ptr,
                    SysMemPitch = (uint)(worldW * 4),
                    SysMemSlicePitch = 0,
                };
                ID3D11Texture2D* auxTex = null;
                _device->CreateTexture2D(&desc, &sub, &auxTex);
                _terrainAuxTexture = auxTex;
            }
            ID3D11ShaderResourceView* auxSrv = null;
            _device->CreateShaderResourceView((ID3D11Resource*)_terrainAuxTexture, null, &auxSrv);
            _terrainAuxSrv = auxSrv;
            _terrainAuxW = worldW;
            _terrainAuxH = worldH;
            return;
        }

        if (dirtyRect is not Rect rect)
            return;
        RectMath.GetMinMaxInclusive(rect, out int minX, out int minY, out int maxX, out int maxY);
        int rw = maxX - minX + 1;
        int rh = maxY - minY + 1;
        if (rw <= 0 || rh <= 0) return;
        var box = new Box((uint)minX, (uint)minY, 0u, (uint)(maxX + 1), (uint)(maxY + 1), 1u);
        byte[] scratch = new byte[rw * rh * 4];
        for (int y = 0; y < rh; y++)
        {
            int src = ((minY + y) * worldW + minX) * 4;
            int dst = y * rw * 4;
            Array.Copy(auxData, src, scratch, dst, rw * 4);
        }
        fixed (byte* ptr = scratch)
        {
            _context->UpdateSubresource((ID3D11Resource*)_terrainAuxTexture, 0, &box, ptr, (uint)(rw * 4), 0);
        }
    }

    private void RunPostProcessPass(in GamePixelsUpload upload)
    {
        if (_postRtv == null || _sceneSrv == null || _postPs == null || _postParamsBuffer == null)
        {
            _displaySrv = _sceneSrv;
            return;
        }

        long t0 = Stopwatch.GetTimestamp();
        PrepareFullscreenPass(_postRtv, _sceneTexW, _sceneTexH, _postPs);
        _profilePostSetupTicks += Stopwatch.GetTimestamp() - t0;

        ID3D11ShaderResourceView* sceneSrv = _sceneSrv;
        _context->PSSetShaderResources(0, 1, &sceneSrv);
        ID3D11ShaderResourceView* auxSrv = _terrainAuxSrv;
        _context->PSSetShaderResources(1, 1, &auxSrv);

        t0 = Stopwatch.GetTimestamp();
        UpdatePostParamsBuffer(upload);
        ID3D11Buffer* cb = _postParamsBuffer;
        _context->PSSetConstantBuffers(0, 1, &cb);
        _profilePostCbUpdateTicks += Stopwatch.GetTimestamp() - t0;
        t0 = Stopwatch.GetTimestamp();
        _context->Draw(3, 0);
        _profilePostDrawTicks += Stopwatch.GetTimestamp() - t0;

        ID3D11ShaderResourceView* nullSrv = null;
        _context->PSSetShaderResources(0, 1, &nullSrv);
        _context->PSSetShaderResources(1, 1, &nullSrv);
        _displaySrv = _postSrv != null ? _postSrv : _sceneSrv;
    }

    private void FlushDetailedProfile()
    {
        double inv = 1000.0 / Stopwatch.Frequency;
        double sceneMs = _profileSceneUploadTicks * inv / _profileFrameCount;
        double auxMs = _profileAuxUploadTicks * inv / _profileFrameCount;
        double postMs = _profilePostPassTicks * inv / _profileFrameCount;
        double postSetupMs = _profilePostSetupTicks * inv / _profileFrameCount;
        double postCbMs = _profilePostCbUpdateTicks * inv / _profileFrameCount;
        double postDrawMs = _profilePostDrawTicks * inv / _profileFrameCount;
        double blitMs = _profileFinalBlitTicks * inv / _profileFrameCount;
        double uiMs = _profileUiRenderTicks * inv / _profileFrameCount;
        Console.WriteLine(
            $"[DX11 Profile] sceneUpload={sceneMs:F3}ms auxUpload={auxMs:F3}ms postTotal={postMs:F3}ms " +
            $"postSetup={postSetupMs:F3}ms postCb={postCbMs:F3}ms postDraw={postDrawMs:F3}ms blit={blitMs:F3}ms ui={uiMs:F3}ms");
        _profileSceneUploadTicks = 0;
        _profileAuxUploadTicks = 0;
        _profilePostPassTicks = 0;
        _profilePostSetupTicks = 0;
        _profilePostCbUpdateTicks = 0;
        _profilePostDrawTicks = 0;
        _profileFinalBlitTicks = 0;
        _profileUiRenderTicks = 0;
        _profileFrameCount = 0;
    }

    private void PrepareFullscreenPass(ID3D11RenderTargetView* target, int width, int height, ID3D11PixelShader* pixelShader)
    {
        ID3D11RenderTargetView* rtv = target;
        _context->OMSetRenderTargets(1, &rtv, null);
        _context->OMSetBlendState(null, null, 0xFFFFFFFFu);
        _context->OMSetDepthStencilState(null, 0);
        _context->RSSetState(null); // Ensure previous ImGui scissor-enabled state does not leak.
        var viewport = new Viewport(0, 0, (uint)Math.Max(1, width), (uint)Math.Max(1, height), 0f, 1f);
        _context->RSSetViewports(1, &viewport);
        _context->IASetInputLayout(null);
        _context->IASetPrimitiveTopology(D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist);
        _context->VSSetShader(_fullscreenVs, null, 0);
        _context->PSSetShader(pixelShader, null, 0);
        ID3D11SamplerState* sampler = _fullscreenSampler;
        _context->PSSetSamplers(0, 1, &sampler);
    }

    private void UpdatePostParamsBuffer(in GamePixelsUpload upload)
    {
        if (_postParamsBuffer == null)
            return;
        PostParamsCBuffer cbData = default;
        cbData.TexelSizeX = 1f / Math.Max(1, upload.View.ViewSize.X);
        cbData.TexelSizeY = 1f / Math.Max(1, upload.View.ViewSize.Y);
        cbData.PixelScale = upload.View.PixelScale;
        cbData.Time = (float)ImGui.GetTime();
        cbData.WorldSizeX = upload.View.WorldSize.X;
        cbData.WorldSizeY = upload.View.WorldSize.Y;
        cbData.CameraPixelsX = upload.View.CameraPixels.X;
        cbData.CameraPixelsY = upload.View.CameraPixels.Y;
        cbData.ViewSizeX = upload.View.ViewSize.X;
        cbData.ViewSizeY = upload.View.ViewSize.Y;
        cbData.UseTerrainAux = upload.TerrainAux != null ? 1f : 0f;
        cbData.BloomThreshold = Tweaks.Screen.PostBloomThreshold;
        cbData.BloomStrength = Tweaks.Screen.PostBloomStrength;
        cbData.BloomWeightCenter = Tweaks.Screen.PostBloomWeightCenter;
        cbData.BloomWeightAxis = Tweaks.Screen.PostBloomWeightAxis;
        cbData.BloomWeightDiagonal = Tweaks.Screen.PostBloomWeightDiagonal;
        cbData.VignetteStrength = Tweaks.Screen.PostVignetteStrength;
        cbData.EdgeLightStrength = Tweaks.Screen.PostTerrainEdgeLightStrength;
        cbData.EdgeLightBias = Tweaks.Screen.PostTerrainEdgeLightBias;
        cbData.TankHeatGlowR = Tweaks.Screen.PostTankHeatGlowR;
        cbData.TankHeatGlowG = Tweaks.Screen.PostTankHeatGlowG;
        cbData.TankHeatGlowB = Tweaks.Screen.PostTankHeatGlowB;
        cbData.TankHeatGlowA = 0f;
        cbData.TerrainHeatGlowR = Tweaks.Screen.PostTerrainHeatGlowR;
        cbData.TerrainHeatGlowG = Tweaks.Screen.PostTerrainHeatGlowG;
        cbData.TerrainHeatGlowB = Tweaks.Screen.PostTerrainHeatGlowB;
        cbData.TerrainHeatThreshold = Tweaks.Screen.PostTerrainHeatThreshold;
        cbData.TerrainMaskEdgeStrength = Tweaks.Screen.PostTerrainMaskEdgeStrength;
        cbData.TerrainMaskCaveDarken = Tweaks.Screen.PostTerrainMaskCaveDarken;
        cbData.TerrainMaskSolidLift = Tweaks.Screen.PostTerrainMaskSolidLift;
        cbData.TerrainMaskOutlineDarken = Tweaks.Screen.PostTerrainMaskOutlineDarken;
        cbData.TerrainMaskRimLift = Tweaks.Screen.PostTerrainMaskRimLift;
        cbData.TerrainMaskBoundaryScale = Tweaks.Screen.PostTerrainMaskBoundaryScale;
        cbData.VignetteInnerRadius = Tweaks.Screen.PostVignetteInnerRadius;
        cbData.VignetteOuterRadius = Tweaks.Screen.PostVignetteOuterRadius;
        cbData.Quality = (float)upload.Quality;
        cbData.MaterialEnergyR = Tweaks.Screen.PostMaterialEmissiveEnergyR;
        cbData.MaterialEnergyG = Tweaks.Screen.PostMaterialEmissiveEnergyG;
        cbData.MaterialEnergyB = Tweaks.Screen.PostMaterialEmissiveEnergyB;
        cbData.MaterialEnergyStrength = Tweaks.Screen.PostMaterialEmissiveEnergyStrength;
        cbData.MaterialScorchedR = Tweaks.Screen.PostMaterialEmissiveScorchedR;
        cbData.MaterialScorchedG = Tweaks.Screen.PostMaterialEmissiveScorchedG;
        cbData.MaterialScorchedB = Tweaks.Screen.PostMaterialEmissiveScorchedB;
        cbData.MaterialScorchedStrength = Tweaks.Screen.PostMaterialEmissiveScorchedStrength;
        cbData.MaterialPulseFreq = Tweaks.Screen.PostMaterialEmissivePulseFreq;
        cbData.MaterialPulseMin = Tweaks.Screen.PostMaterialEmissivePulseMin;
        cbData.MaterialPulseRange = Tweaks.Screen.PostMaterialEmissivePulseRange;
        cbData.MaterialPulsePad = 0f;
        int glowCount = Math.Clamp(upload.TankHeatGlowCount, 0, 8);
        cbData.TankGlowCount = glowCount;
        if (upload.TankHeatGlowData != null)
        {
            for (int i = 0; i < glowCount; i++)
            {
                int src = i * 4;
                int dst = i * 4;
                cbData.TankGlow[dst + 0] = upload.TankHeatGlowData[src + 0];
                cbData.TankGlow[dst + 1] = upload.TankHeatGlowData[src + 1];
                cbData.TankGlow[dst + 2] = upload.TankHeatGlowData[src + 2];
                cbData.TankGlow[dst + 3] = upload.TankHeatGlowData[src + 3];
            }
        }

        MappedSubresource mapped;
        if (_context->Map((ID3D11Resource*)_postParamsBuffer, 0, Silk.NET.Direct3D11.Map.WriteDiscard, 0, &mapped) >= 0)
        {
            *(PostParamsCBuffer*)mapped.PData = cbData;
            _context->Unmap((ID3D11Resource*)_postParamsBuffer, 0);
        }
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

    // ── Pixel post-processing (CPU fallback path) ──────────────────────

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

    private unsafe struct PostParamsCBuffer
    {
        public float TexelSizeX, TexelSizeY, PixelScale, Time;
        public float WorldSizeX, WorldSizeY, CameraPixelsX, CameraPixelsY;
        public float ViewSizeX, ViewSizeY, UseTerrainAux, BloomThreshold;
        public float BloomStrength, BloomWeightCenter, BloomWeightAxis, BloomWeightDiagonal;
        public float VignetteStrength, EdgeLightStrength, EdgeLightBias, TankHeatGlowR;
        public float TankHeatGlowG, TankHeatGlowB, TankHeatGlowA, TerrainHeatGlowR;
        public float TerrainHeatGlowG, TerrainHeatGlowB, TerrainHeatThreshold, TerrainMaskEdgeStrength;
        public float TerrainMaskCaveDarken,
                     TerrainMaskSolidLift,
                     TerrainMaskOutlineDarken,
                     TerrainMaskRimLift;
        public float TerrainMaskBoundaryScale, VignetteInnerRadius, VignetteOuterRadius, Quality;
        public float MaterialEnergyR, MaterialEnergyG, MaterialEnergyB, MaterialEnergyStrength;
        public float MaterialScorchedR, MaterialScorchedG, MaterialScorchedB, MaterialScorchedStrength;
        public float MaterialPulseFreq, MaterialPulseMin, MaterialPulseRange, MaterialPulsePad;
        public float TankGlowCount, _pad0, _pad1, _pad2;
        public fixed float TankGlow[32];
    }
}
