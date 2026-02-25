namespace TunnelTanks.Desktop.Rendering;

using Silk.NET.OpenGL;
using StbImageSharp;

/// <summary>
/// Loads PNG images from disk and creates OpenGL textures suitable for use with ImGui.
/// All created textures are tracked for cleanup on Dispose.
/// </summary>
public sealed unsafe class TextureManager : IDisposable
{
    private readonly GL _gl;
    private readonly List<uint> _textures = [];
    private bool _disposed;

    public TextureManager(GL gl)
    {
        _gl = gl;
        StbImage.stbi_set_flip_vertically_on_load(0);
    }

    /// <summary>
    /// Load a PNG/JPG from disk and return the GL texture ID (castable to nint for ImGui).
    /// </summary>
    public nint LoadTexture(string path)
    {
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        return CreateGlTexture(image.Data, image.Width, image.Height);
    }

    /// <summary>
    /// Create a GL texture from raw RGBA pixel data.
    /// </summary>
    public nint LoadTextureFromRgba(byte[] rgba, int width, int height)
    {
        return CreateGlTexture(rgba, width, height);
    }

    private nint CreateGlTexture(byte[] pixels, int width, int height)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        fixed (byte* ptr = pixels)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, 0,
                Silk.NET.OpenGL.PixelFormat.Rgba, Silk.NET.OpenGL.PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _textures.Add(tex);
        return (nint)tex;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (uint tex in _textures)
            _gl.DeleteTexture(tex);
        _textures.Clear();
    }
}
