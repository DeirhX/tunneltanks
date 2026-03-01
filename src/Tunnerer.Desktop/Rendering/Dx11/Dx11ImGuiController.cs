namespace Tunnerer.Desktop.Rendering.Dx11;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ImGuiNET;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using Silk.NET.SDL;

/// <summary>
/// SDL2 + DX11 Dear ImGui bridge (input + UI draw only).
/// </summary>
public sealed unsafe class ImGuiController : IDisposable
{
    private readonly Sdl _sdl;
    private readonly Window* _window;
    private readonly ID3D11Device* _device;
    private readonly ID3D11DeviceContext* _context;

    private ID3D11Buffer* _vertexBuffer;
    private ID3D11Buffer* _indexBuffer;
    private int _vertexBufferSize = 5000;
    private int _indexBufferSize = 10000;

    private ID3D11VertexShader* _vertexShader;
    private ID3D11PixelShader* _pixelShader;
    private ID3D11InputLayout* _inputLayout;
    private ID3D11Buffer* _vertexConstantBuffer;
    private ID3D11SamplerState* _fontSampler;
    private ID3D11ShaderResourceView* _fontTextureView;
    private ID3D11RasterizerState* _rasterizerState;
    private ID3D11BlendState* _blendState;
    private ID3D11DepthStencilState* _depthStencilState;

    private bool _disposed;

    public ImGuiController(Sdl sdl, Window* window, ID3D11Device* device, ID3D11DeviceContext* context, int windowW, int windowH)
    {
        _sdl = sdl;
        _window = window;
        _device = device;
        _context = context;

        var imguiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(imguiContext);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.DisplaySize = new Vector2(windowW, windowH);
        io.DisplayFramebufferScale = Vector2.One;

        ImGui.StyleColorsDark();
        CreateDeviceObjects();
    }

    public void ProcessEvent(Event ev)
    {
        var io = ImGui.GetIO();
        switch ((EventType)ev.Type)
        {
            case EventType.Mousemotion:
                io.AddMousePosEvent(ev.Motion.X, ev.Motion.Y);
                break;
            case EventType.Mousebuttondown:
            case EventType.Mousebuttonup:
            {
                int button = ev.Button.Button switch
                {
                    1 => 0,
                    2 => 2,
                    3 => 1,
                    _ => -1
                };
                if (button >= 0)
                    io.AddMouseButtonEvent(button, ev.Type == (uint)EventType.Mousebuttondown);
                break;
            }
            case EventType.Mousewheel:
                io.AddMouseWheelEvent(ev.Wheel.X, ev.Wheel.Y);
                break;
            case EventType.Textinput:
            {
                string text = Marshal.PtrToStringUTF8((nint)ev.Text.Text) ?? "";
                foreach (char c in text)
                    io.AddInputCharacter(c);
                break;
            }
            case EventType.Keydown:
            case EventType.Keyup:
            {
                bool down = ev.Type == (uint)EventType.Keydown;
                var key = SdlScancodeToImGuiKey((Scancode)ev.Key.Keysym.Scancode);
                if (key != ImGuiKey.None)
                    io.AddKeyEvent(key, down);

                io.AddKeyEvent(ImGuiKey.ModCtrl, (ev.Key.Keysym.Mod & (ushort)Keymod.Ctrl) != 0);
                io.AddKeyEvent(ImGuiKey.ModShift, (ev.Key.Keysym.Mod & (ushort)Keymod.Shift) != 0);
                io.AddKeyEvent(ImGuiKey.ModAlt, (ev.Key.Keysym.Mod & (ushort)Keymod.Alt) != 0);
                break;
            }
        }
    }

    public void NewFrame(int windowW, int windowH, float deltaTime)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(windowW, windowH);
        io.DisplayFramebufferScale = Vector2.One;
        io.DeltaTime = deltaTime > 0 ? deltaTime : 1f / 60f;
        ImGui.NewFrame();
    }

    public void Render(ID3D11RenderTargetView* backbufferRtv, int fbWidth, int fbHeight)
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData(), backbufferRtv, fbWidth, fbHeight);
    }

    private void RenderDrawData(ImDrawDataPtr drawData, ID3D11RenderTargetView* backbufferRtv, int fbWidth, int fbHeight)
    {
        if (drawData.CmdListsCount <= 0 || fbWidth <= 0 || fbHeight <= 0)
            return;

        EnsureBuffers(drawData.TotalVtxCount, drawData.TotalIdxCount);

        if (!UploadBuffers(drawData))
            return;

        ID3D11RenderTargetView* rtv = backbufferRtv;
        _context->OMSetRenderTargets(1, &rtv, null);
        var vp = new Viewport(0, 0, (uint)fbWidth, (uint)fbHeight, 0f, 1f);
        _context->RSSetViewports(1, &vp);

        SetupRenderState(drawData);

        int globalVtxOffset = 0;
        int globalIdxOffset = 0;
        Vector2 clipOff = drawData.DisplayPos;
        Vector2 clipScale = drawData.FramebufferScale;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
            {
                var pcmd = cmdList.CmdBuffer[cmdi];
                if (pcmd.UserCallback != nint.Zero)
                    continue;

                Vector4 clipRect = new(
                    (pcmd.ClipRect.X - clipOff.X) * clipScale.X,
                    (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y,
                    (pcmd.ClipRect.Z - clipOff.X) * clipScale.X,
                    (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y);
                if (clipRect.X < 0.0f) clipRect.X = 0.0f;
                if (clipRect.Y < 0.0f) clipRect.Y = 0.0f;
                if (clipRect.Z > fbWidth) clipRect.Z = fbWidth;
                if (clipRect.W > fbHeight) clipRect.W = fbHeight;
                if (clipRect.X >= clipRect.Z || clipRect.Y >= clipRect.W)
                    continue;

                var r = new Box2D<int>(
                    (int)clipRect.X,
                    (int)clipRect.Y,
                    (int)clipRect.Z,
                    (int)clipRect.W);
                _context->RSSetScissorRects(1, &r);

                ID3D11ShaderResourceView* textureSrv = (ID3D11ShaderResourceView*)pcmd.TextureId;
                _context->PSSetShaderResources(0, 1, &textureSrv);

                uint startIndex = (uint)(pcmd.IdxOffset + globalIdxOffset);
                int baseVertex = (int)(pcmd.VtxOffset + globalVtxOffset);
                _context->DrawIndexed(pcmd.ElemCount, startIndex, baseVertex);
            }

            globalIdxOffset += cmdList.IdxBuffer.Size;
            globalVtxOffset += cmdList.VtxBuffer.Size;
        }

        ID3D11ShaderResourceView* nullSrv = null;
        _context->PSSetShaderResources(0, 1, &nullSrv);
    }

    private void SetupRenderState(ImDrawDataPtr drawData)
    {
        uint stride = (uint)Unsafe.SizeOf<ImDrawVert>();
        uint offset = 0;
        ID3D11Buffer* vb = _vertexBuffer;
        _context->IASetVertexBuffers(0, 1, &vb, &stride, &offset);
        _context->IASetIndexBuffer(_indexBuffer, Format.FormatR16Uint, 0);
        _context->IASetInputLayout(_inputLayout);
        _context->IASetPrimitiveTopology(D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist);

        _context->VSSetShader(_vertexShader, null, 0);
        ID3D11Buffer* cb = _vertexConstantBuffer;
        _context->VSSetConstantBuffers(0, 1, &cb);
        _context->PSSetShader(_pixelShader, null, 0);
        ID3D11SamplerState* sampler = _fontSampler;
        _context->PSSetSamplers(0, 1, &sampler);

        ID3D11BlendState* blend = _blendState;
        _context->OMSetBlendState(blend, null, 0xFFFFFFFFu);
        _context->OMSetDepthStencilState(_depthStencilState, 0);
        _context->RSSetState(_rasterizerState);

        var cbData = default(VertexConstants);
        float l = drawData.DisplayPos.X;
        float r = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float t = drawData.DisplayPos.Y;
        float b = drawData.DisplayPos.Y + drawData.DisplaySize.Y;
        cbData.M00 = 2.0f / (r - l);
        cbData.M11 = 2.0f / (t - b);
        cbData.M22 = 0.5f;
        cbData.M30 = (r + l) / (l - r);
        cbData.M31 = (t + b) / (b - t);
        cbData.M32 = 0.5f;
        cbData.M33 = 1.0f;

        MappedSubresource mapped;
        if (_context->Map((ID3D11Resource*)_vertexConstantBuffer, 0, Silk.NET.Direct3D11.Map.WriteDiscard, 0, &mapped) >= 0)
        {
            *(VertexConstants*)mapped.PData = cbData;
            _context->Unmap((ID3D11Resource*)_vertexConstantBuffer, 0);
        }
    }

    private bool UploadBuffers(ImDrawDataPtr drawData)
    {
        MappedSubresource vbMap;
        if (_context->Map((ID3D11Resource*)_vertexBuffer, 0, Silk.NET.Direct3D11.Map.WriteDiscard, 0, &vbMap) < 0)
            return false;
        MappedSubresource ibMap;
        if (_context->Map((ID3D11Resource*)_indexBuffer, 0, Silk.NET.Direct3D11.Map.WriteDiscard, 0, &ibMap) < 0)
        {
            _context->Unmap((ID3D11Resource*)_vertexBuffer, 0);
            return false;
        }

        ImDrawVert* vtxDst = (ImDrawVert*)vbMap.PData;
        ushort* idxDst = (ushort*)ibMap.PData;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            nuint vtxBytes = (nuint)(cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>());
            nuint idxBytes = (nuint)(cmdList.IdxBuffer.Size * sizeof(ushort));
            Buffer.MemoryCopy((void*)cmdList.VtxBuffer.Data, vtxDst, vtxBytes, vtxBytes);
            Buffer.MemoryCopy((void*)cmdList.IdxBuffer.Data, idxDst, idxBytes, idxBytes);
            vtxDst += cmdList.VtxBuffer.Size;
            idxDst += cmdList.IdxBuffer.Size;
        }

        _context->Unmap((ID3D11Resource*)_vertexBuffer, 0);
        _context->Unmap((ID3D11Resource*)_indexBuffer, 0);
        return true;
    }

    private void EnsureBuffers(int totalVtxCount, int totalIdxCount)
    {
        if (_vertexBuffer == null || totalVtxCount > _vertexBufferSize)
        {
            if (_vertexBuffer != null) { _vertexBuffer->Release(); _vertexBuffer = null; }
            _vertexBufferSize = totalVtxCount + 5000;
            var desc = new BufferDesc
            {
                Usage = Usage.Dynamic,
                ByteWidth = (uint)(_vertexBufferSize * Unsafe.SizeOf<ImDrawVert>()),
                BindFlags = (uint)BindFlag.VertexBuffer,
                CPUAccessFlags = (uint)CpuAccessFlag.Write,
                MiscFlags = 0,
                StructureByteStride = 0,
            };
            ID3D11Buffer* vb = null;
            _device->CreateBuffer(&desc, null, &vb);
            _vertexBuffer = vb;
        }

        if (_indexBuffer == null || totalIdxCount > _indexBufferSize)
        {
            if (_indexBuffer != null) { _indexBuffer->Release(); _indexBuffer = null; }
            _indexBufferSize = totalIdxCount + 10000;
            var desc = new BufferDesc
            {
                Usage = Usage.Dynamic,
                ByteWidth = (uint)(_indexBufferSize * sizeof(ushort)),
                BindFlags = (uint)BindFlag.IndexBuffer,
                CPUAccessFlags = (uint)CpuAccessFlag.Write,
                MiscFlags = 0,
                StructureByteStride = 0,
            };
            ID3D11Buffer* ib = null;
            _device->CreateBuffer(&desc, null, &ib);
            _indexBuffer = ib;
        }
    }

    private void CreateDeviceObjects()
    {
        const string vertexShaderSrc = @"
cbuffer vertexBuffer : register(b0)
{
    float4x4 ProjectionMatrix;
};

struct VS_INPUT
{
    float2 pos : POSITION;
    float2 uv  : TEXCOORD0;
    float4 col : COLOR0;
};

struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};

PS_INPUT main(VS_INPUT input)
{
    PS_INPUT output;
    output.pos = mul(ProjectionMatrix, float4(input.pos.xy, 0.f, 1.f));
    output.col = input.col;
    output.uv  = input.uv;
    return output;
}";

        const string pixelShaderSrc = @"
struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};

SamplerState sampler0 : register(s0);
Texture2D texture0 : register(t0);

float4 main(PS_INPUT input) : SV_Target
{
    float4 out_col = input.col * texture0.Sample(sampler0, input.uv);
    return out_col;
}";

        ID3DBlob* vertexBlob = CompileShader(vertexShaderSrc, "main", "vs_5_0");
        ID3DBlob* pixelBlob = CompileShader(pixelShaderSrc, "main", "ps_5_0");
        if (vertexBlob == null || pixelBlob == null)
            throw new Exception("Failed to compile DX11 ImGui shaders.");

        ID3D11VertexShader* vs = null;
        ID3D11PixelShader* ps = null;
        if (_device->CreateVertexShader(BlobGetBufferPointer(vertexBlob), BlobGetBufferSize(vertexBlob), null, &vs) < 0 ||
            _device->CreatePixelShader(BlobGetBufferPointer(pixelBlob), BlobGetBufferSize(pixelBlob), null, &ps) < 0)
        {
            BlobRelease(vertexBlob);
            BlobRelease(pixelBlob);
            throw new Exception("Failed to create DX11 ImGui shaders.");
        }
        _vertexShader = vs;
        _pixelShader = ps;

        var position = SilkMarshal.StringToPtr("POSITION");
        var texcoord = SilkMarshal.StringToPtr("TEXCOORD");
        var color = SilkMarshal.StringToPtr("COLOR");
        try
        {
            var localLayout = stackalloc InputElementDesc[3];
            localLayout[0] = new InputElementDesc
            {
                SemanticName = (byte*)position,
                SemanticIndex = 0,
                Format = Format.FormatR32G32Float,
                InputSlot = 0,
                AlignedByteOffset = 0,
                InputSlotClass = InputClassification.PerVertexData,
                InstanceDataStepRate = 0,
            };
            localLayout[1] = new InputElementDesc
            {
                SemanticName = (byte*)texcoord,
                SemanticIndex = 0,
                Format = Format.FormatR32G32Float,
                InputSlot = 0,
                AlignedByteOffset = 8,
                InputSlotClass = InputClassification.PerVertexData,
                InstanceDataStepRate = 0,
            };
            localLayout[2] = new InputElementDesc
            {
                SemanticName = (byte*)color,
                SemanticIndex = 0,
                Format = Format.FormatR8G8B8A8Unorm,
                InputSlot = 0,
                AlignedByteOffset = 16,
                InputSlotClass = InputClassification.PerVertexData,
                InstanceDataStepRate = 0,
            };
            ID3D11InputLayout* inputLayout = null;
            if (_device->CreateInputLayout(localLayout, 3, BlobGetBufferPointer(vertexBlob), BlobGetBufferSize(vertexBlob), &inputLayout) < 0)
                throw new Exception("Failed to create DX11 ImGui input layout.");
            _inputLayout = inputLayout;
        }
        finally
        {
            SilkMarshal.Free((nint)position);
            SilkMarshal.Free((nint)texcoord);
            SilkMarshal.Free((nint)color);
        }

        BlobRelease(vertexBlob);
        BlobRelease(pixelBlob);

        var vbDesc = new BufferDesc
        {
            ByteWidth = (uint)Unsafe.SizeOf<VertexConstants>(),
            Usage = Usage.Dynamic,
            BindFlags = (uint)BindFlag.ConstantBuffer,
            CPUAccessFlags = (uint)CpuAccessFlag.Write,
            MiscFlags = 0,
            StructureByteStride = 0,
        };
        ID3D11Buffer* vertexConstantBuffer = null;
        if (_device->CreateBuffer(&vbDesc, null, &vertexConstantBuffer) < 0)
            throw new Exception("Failed to create DX11 ImGui constant buffer.");
        _vertexConstantBuffer = vertexConstantBuffer;

        var samplerDesc = new SamplerDesc
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            ComparisonFunc = ComparisonFunc.Always,
            MinLOD = 0,
            MaxLOD = 0,
            MipLODBias = 0,
            MaxAnisotropy = 0,
        };
        ID3D11SamplerState* fontSampler = null;
        if (_device->CreateSamplerState(&samplerDesc, &fontSampler) < 0)
            throw new Exception("Failed to create DX11 ImGui sampler.");
        _fontSampler = fontSampler;

        var rasterDesc = new RasterizerDesc
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,
            ScissorEnable = 1,
            DepthClipEnable = 1,
        };
        ID3D11RasterizerState* rasterizerState = null;
        if (_device->CreateRasterizerState(&rasterDesc, &rasterizerState) < 0)
            throw new Exception("Failed to create DX11 ImGui rasterizer state.");
        _rasterizerState = rasterizerState;

        var depthDesc = new DepthStencilDesc
        {
            DepthEnable = 0,
            DepthWriteMask = DepthWriteMask.Zero,
            DepthFunc = ComparisonFunc.Always,
            StencilEnable = 0,
        };
        ID3D11DepthStencilState* depthStencilState = null;
        if (_device->CreateDepthStencilState(&depthDesc, &depthStencilState) < 0)
            throw new Exception("Failed to create DX11 ImGui depth state.");
        _depthStencilState = depthStencilState;

        CreateFontTexture();
    }

    private void CreateFontTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out _);

        var desc = new Texture2DDesc
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatR8G8B8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.ShaderResource,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };
        var sub = new SubresourceData
        {
            PSysMem = pixels,
            SysMemPitch = (uint)(width * 4),
            SysMemSlicePitch = 0,
        };
        ID3D11Texture2D* tex = null;
        if (_device->CreateTexture2D(&desc, &sub, &tex) < 0 || tex == null)
            throw new Exception("Failed to create DX11 ImGui font texture.");
        ID3D11ShaderResourceView* fontTextureView = null;
        if (_device->CreateShaderResourceView((ID3D11Resource*)tex, null, &fontTextureView) < 0 || fontTextureView == null)
        {
            tex->Release();
            throw new Exception("Failed to create DX11 ImGui font SRV.");
        }
        _fontTextureView = fontTextureView;
        tex->Release();
        io.Fonts.SetTexID((nint)_fontTextureView);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ImGui.DestroyContext();

        if (_vertexBuffer != null) { _vertexBuffer->Release(); _vertexBuffer = null; }
        if (_indexBuffer != null) { _indexBuffer->Release(); _indexBuffer = null; }
        if (_vertexShader != null) { _vertexShader->Release(); _vertexShader = null; }
        if (_pixelShader != null) { _pixelShader->Release(); _pixelShader = null; }
        if (_inputLayout != null) { _inputLayout->Release(); _inputLayout = null; }
        if (_vertexConstantBuffer != null) { _vertexConstantBuffer->Release(); _vertexConstantBuffer = null; }
        if (_fontSampler != null) { _fontSampler->Release(); _fontSampler = null; }
        if (_fontTextureView != null) { _fontTextureView->Release(); _fontTextureView = null; }
        if (_rasterizerState != null) { _rasterizerState->Release(); _rasterizerState = null; }
        if (_blendState != null) { _blendState->Release(); _blendState = null; }
        if (_depthStencilState != null) { _depthStencilState->Release(); _depthStencilState = null; }
    }

    private static ImGuiKey SdlScancodeToImGuiKey(Scancode sc) => sc switch
    {
        Scancode.ScancodeTab => ImGuiKey.Tab,
        Scancode.ScancodeLeft => ImGuiKey.LeftArrow,
        Scancode.ScancodeRight => ImGuiKey.RightArrow,
        Scancode.ScancodeUp => ImGuiKey.UpArrow,
        Scancode.ScancodeDown => ImGuiKey.DownArrow,
        Scancode.ScancodePageup => ImGuiKey.PageUp,
        Scancode.ScancodePagedown => ImGuiKey.PageDown,
        Scancode.ScancodeHome => ImGuiKey.Home,
        Scancode.ScancodeEnd => ImGuiKey.End,
        Scancode.ScancodeInsert => ImGuiKey.Insert,
        Scancode.ScancodeDelete => ImGuiKey.Delete,
        Scancode.ScancodeBackspace => ImGuiKey.Backspace,
        Scancode.ScancodeSpace => ImGuiKey.Space,
        Scancode.ScancodeReturn => ImGuiKey.Enter,
        Scancode.ScancodeEscape => ImGuiKey.Escape,
        Scancode.ScancodeA => ImGuiKey.A,
        Scancode.ScancodeC => ImGuiKey.C,
        Scancode.ScancodeV => ImGuiKey.V,
        Scancode.ScancodeX => ImGuiKey.X,
        Scancode.ScancodeY => ImGuiKey.Y,
        Scancode.ScancodeZ => ImGuiKey.Z,
        _ => ImGuiKey.None
    };

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
                    BlobRelease(errors);
                    throw new Exception($"DX11 ImGui shader compile failed: {msg}");
                }
                throw new Exception("DX11 ImGui shader compile failed.");
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

    [StructLayout(LayoutKind.Sequential)]
    private struct VertexConstants
    {
        public float M00, M01, M02, M03;
        public float M10, M11, M12, M13;
        public float M20, M21, M22, M23;
        public float M30, M31, M32, M33;
    }
}
