namespace Tunnerer.Desktop.Rendering;

using System;
using Silk.NET.SDL;
using Tunnerer.Core.Types;

public readonly record struct RenderView(
    Size ViewSize,
    Size WorldSize,
    Position CameraPixels,
    int PixelScale);

[Flags]
public enum PostProcessPassFlags : byte
{
    None = 0,
    Bloom = 1 << 0,
    Vignette = 1 << 1,
    EdgeLift = 1 << 2,
    TerrainCurve = 1 << 3,
    TerrainAux = 1 << 4,
    TankGlow = 1 << 5,
    All = Bloom | Vignette | EdgeLift | TerrainCurve | TerrainAux | TankGlow,
}

public readonly record struct GamePixelsUpload(
    uint[] Pixels,
    RenderView View,
    PostProcessUploadOptions PostProcess,
    TankGlowUpload TankGlow,
    TerrainAuxUpload TerrainAux,
    NativeContinuousUpload NativeContinuous);

public readonly record struct PostProcessUploadOptions(
    HiResRenderQuality Quality,
    bool HeatDebugOverlayEnabled,
    PostProcessPassFlags PassFlags);

public readonly record struct TankGlowUpload(
    float[]? Data,
    int Count);

public readonly record struct TerrainAuxUpload(
    byte[]? Data,
    Rect? DirtyRect);

public readonly record struct NativeContinuousUpload(
    bool Enabled,
    uint[]? SourcePixels,
    int SampleCount);

public interface IGameRenderBackend : IDisposable
{
    nint GameTextureId { get; }
    Size GameTextureSize { get; }
    bool SupportsUi { get; }

    void ProcessEvent(Event ev);

    void UploadGamePixels(in GamePixelsUpload upload);

    void ClearFrame(Size viewportSize, Tunnerer.Core.Types.Color clearColor);

    void NewFrame(int windowW, int windowH, float deltaTime);

    void Render();

    void RequestScreenshot(string? label = null);
}
