namespace Tunnerer.Desktop.Config;

public enum RenderBackendKind
{
    Dx11 = 0,
    Dx12 = 1,
}

public static class DesktopTweaks
{
    public const RenderBackendKind DefaultRenderBackend = RenderBackendKind.Dx11;
}
