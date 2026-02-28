namespace Tunnerer.Desktop.Rendering;

/// <summary>
/// Minimal DX11 texture loader skeleton.
/// Returns placeholder IDs until DX11 SRV-backed ImGui texture binding is implemented.
/// </summary>
public sealed class Dx11TextureManager : ITextureLoader
{
    public nint LoadTexture(string path, bool linear = false)
    {
        _ = linear;
        if (!File.Exists(path))
            throw new FileNotFoundException($"Texture file not found: {path}", path);
        return nint.Zero;
    }

    public void Dispose()
    {
    }
}
