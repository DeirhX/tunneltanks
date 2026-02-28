namespace Tunnerer.Desktop.Rendering;

using Silk.NET.OpenGL;
using Silk.NET.SDL;
using Tunnerer.Core.Config;

public static unsafe class RenderBackendFactory
{
    public readonly record struct RenderBackendServices(
        IGameRenderBackend Backend,
        ITextureLoader Textures);

    public static RenderBackendServices CreateServices(RenderBackendKind kind, Sdl sdl, Window* window, int windowW, int windowH)
    {
        EnsureBackendSupported(kind);
        return kind switch
        {
            RenderBackendKind.OpenGl => CreateOpenGlServices(sdl, window, windowW, windowH),
            RenderBackendKind.Dx11 => CreateDx11Services(sdl, window),
            RenderBackendKind.Dx12 => throw new NotSupportedException("DX12 backend selection is wired, but implementation is still pending."),
            _ => throw new NotSupportedException($"Unknown render backend '{kind}'."),
        };
    }

    private static void EnsureBackendSupported(RenderBackendKind kind)
    {
        if ((kind == RenderBackendKind.Dx11 || kind == RenderBackendKind.Dx12) && !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException($"{kind} backend requires Windows.");
    }

    private static RenderBackendServices CreateOpenGlServices(Sdl sdl, Window* window, int windowW, int windowH)
    {
        var gl = GL.GetApi(name => (nint)sdl.GLGetProcAddress(name));
        var imgui = new OpenGlImGuiController(gl, sdl, window, windowW, windowH);
        return new RenderBackendServices(
            Backend: new OpenGlGameRenderBackend(gl, imgui),
            Textures: new OpenGlTextureManager(gl));
    }

    private static RenderBackendServices CreateDx11Services(Sdl sdl, Window* window)
    {
        var backend = new Dx11GameRenderBackend(sdl, window);
        return new RenderBackendServices(
            Backend: backend,
            Textures: new Dx11TextureManager(backend));
    }
}
