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
        return kind switch
        {
            RenderBackendKind.OpenGl => CreateOpenGlServices(sdl, window, windowW, windowH),
            RenderBackendKind.Dx11 => throw new NotSupportedException("DX11 backend is not implemented yet."),
            RenderBackendKind.Dx12 => throw new NotSupportedException("DX12 backend is not implemented yet."),
            _ => throw new NotSupportedException($"Unknown render backend '{kind}'."),
        };
    }

    private static RenderBackendServices CreateOpenGlServices(Sdl sdl, Window* window, int windowW, int windowH)
    {
        var gl = GL.GetApi(name => (nint)sdl.GLGetProcAddress(name));
        var imgui = new ImGuiController(gl, sdl, window, windowW, windowH);
        return new RenderBackendServices(
            Backend: new OpenGlGameRenderBackend(gl, imgui),
            Textures: new TextureManager(gl));
    }
}
