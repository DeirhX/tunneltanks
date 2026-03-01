namespace Tunnerer.Desktop.Rendering.Dx11;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.SDL;

public sealed unsafe partial class Backend
{
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
        _imgui = new ImGuiController(_sdl, _window, _device, _context, winW, winH);
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
        if (!TryGetShaderBytecode("FullscreenVs", "vs_5_0", out byte[]? vsBytecode) || vsBytecode == null)
            return false;

        ID3D11VertexShader* vs = null;
        int hr;
        fixed (byte* pVsBytecode = vsBytecode)
            hr = _device->CreateVertexShader(pVsBytecode, (nuint)vsBytecode.Length, null, &vs);
        if (hr < 0 || vs == null)
            return false;
        _fullscreenVs = vs;

        if (!TryGetShaderBytecode("BlitPs", "ps_5_0", out byte[]? blitBytecode) || blitBytecode == null)
            return false;

        ID3D11PixelShader* ps = null;
        fixed (byte* pBlitBytecode = blitBytecode)
            hr = _device->CreatePixelShader(pBlitBytecode, (nuint)blitBytecode.Length, null, &ps);
        if (hr < 0 || ps == null)
            return false;
        _blitPs = ps;

        if (!TryGetShaderBytecode("PostPs", "ps_5_0", out byte[]? postBytecode) || postBytecode == null)
            return false;

        ID3D11PixelShader* postPs = null;
        fixed (byte* pPostBytecode = postBytecode)
            hr = _device->CreatePixelShader(pPostBytecode, (nuint)postBytecode.Length, null, &postPs);
        if (hr < 0 || postPs == null)
            return false;
        _postPs = postPs;

        if (!TryGetShaderBytecode("TerrainPs", "ps_5_0", out byte[]? terrainBytecode) || terrainBytecode == null)
            return false;

        ID3D11PixelShader* terrainPs = null;
        fixed (byte* pTerrainBytecode = terrainBytecode)
            hr = _device->CreatePixelShader(pTerrainBytecode, (nuint)terrainBytecode.Length, null, &terrainPs);
        if (hr < 0 || terrainPs == null)
            return false;
        _terrainPs = terrainPs;

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
}
