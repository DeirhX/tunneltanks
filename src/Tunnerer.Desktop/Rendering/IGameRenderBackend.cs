namespace Tunnerer.Desktop.Rendering;

using Silk.NET.SDL;

public readonly record struct GamePixelsUpload(
    uint[] Pixels,
    int Width,
    int Height,
    HiResRenderQuality Quality,
    float[]? TankHeatGlowData,
    int TankHeatGlowCount,
    byte[]? TerrainAux,
    int WorldWidth,
    int WorldHeight,
    int CamPixelX,
    int CamPixelY,
    int PixelScale,
    bool HasAuxDirtyRect,
    int AuxMinX,
    int AuxMinY,
    int AuxMaxX,
    int AuxMaxY);

public interface IGameRenderBackend : IDisposable
{
    nint GameTextureId { get; }

    void ProcessEvent(Event ev);

    void UploadGamePixels(in GamePixelsUpload upload);

    void ClearFrame(int width, int height, float r, float g, float b, float a);

    void NewFrame(int windowW, int windowH, float deltaTime);

    void Render();
}
