namespace Tunnerer.Desktop.Rendering;

using Silk.NET.SDL;
using Tunnerer.Core.Types;

public readonly record struct RenderView(
    Size ViewSize,
    Size WorldSize,
    Position CameraPixels,
    int PixelScale);

public readonly record struct GamePixelsUpload(
    uint[] Pixels,
    RenderView View,
    HiResRenderQuality Quality,
    float[]? TankHeatGlowData,
    int TankHeatGlowCount,
    byte[]? TerrainAux,
    Rect? AuxDirtyRect,
    bool UseNativeContinuous,
    uint[]? NativeSourcePixels,
    int NativeSampleCount);

public interface IGameRenderBackend : IDisposable
{
    nint GameTextureId { get; }
    bool SupportsUi { get; }

    void ProcessEvent(Event ev);

    void UploadGamePixels(in GamePixelsUpload upload);

    void ClearFrame(Size viewportSize, Tunnerer.Core.Types.Color clearColor);

    void NewFrame(int windowW, int windowH, float deltaTime);

    void Render();
}
