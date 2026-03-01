namespace Tunnerer.Desktop.Rendering.Dx11;

using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using StbImageSharp;
using Tunnerer.Desktop.Rendering;

/// <summary>
/// Loads PNG/JPG images from disk and creates DX11 SRVs suitable for use with ImGui.
/// Returned texture IDs are raw ID3D11ShaderResourceView pointers cast to nint.
/// </summary>
public sealed unsafe class TextureManager : ITextureLoader
{
    private readonly Backend _backend;
    private readonly List<nint> _srvHandles = [];
    private bool _disposed;

    public TextureManager(Backend backend)
    {
        _backend = backend;
        StbImage.stbi_set_flip_vertically_on_load(0);
    }

    public nint LoadTexture(string path, bool linear = false)
    {
        _ = linear;
        if (!File.Exists(path))
            throw new FileNotFoundException($"Texture file not found: {path}", path);
        if (_backend.Device == null)
            return nint.Zero;

        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        return CreateSrvFromRgba(image.Data, image.Width, image.Height);
    }

    private nint CreateSrvFromRgba(byte[] pixels, int width, int height)
    {
        var device = _backend.Device;
        if (device == null)
            return nint.Zero;

        var texDesc = new Texture2DDesc
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatR8G8B8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Immutable,
            BindFlags = (uint)BindFlag.ShaderResource,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        fixed (byte* ptr = pixels)
        {
            var init = new SubresourceData
            {
                PSysMem = ptr,
                SysMemPitch = (uint)(width * 4),
                SysMemSlicePitch = 0,
            };

            ID3D11Texture2D* tex = null;
            int hr = device->CreateTexture2D(&texDesc, &init, &tex);
            if (hr < 0 || tex == null)
                throw new Exception($"DX11 CreateTexture2D failed (hr=0x{hr:X8}) for '{width}x{height}'.");

            ID3D11ShaderResourceView* srv = null;
            hr = device->CreateShaderResourceView((ID3D11Resource*)tex, null, &srv);
            tex->Release();
            if (hr < 0 || srv == null)
                throw new Exception($"DX11 CreateShaderResourceView failed (hr=0x{hr:X8}).");

            nint handle = (nint)srv;
            _srvHandles.Add(handle);
            return handle;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = 0; i < _srvHandles.Count; i++)
        {
            ID3D11ShaderResourceView* srv = (ID3D11ShaderResourceView*)_srvHandles[i];
            if (srv != null)
                srv->Release();
        }
        _srvHandles.Clear();
    }
}
