namespace Tunnerer.Desktop.Rendering;

public interface ITextureLoader : IDisposable
{
    nint LoadTexture(string path, bool linear = false);
}
