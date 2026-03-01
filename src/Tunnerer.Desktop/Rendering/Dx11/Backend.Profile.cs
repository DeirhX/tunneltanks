namespace Tunnerer.Desktop.Rendering.Dx11;

using System.Diagnostics;

public sealed unsafe partial class Backend
{
    private void FlushDetailedProfile()
    {
        double inv = 1000.0 / Stopwatch.Frequency;
        double sceneMs = _profileSceneUploadTicks * inv / _profileFrameCount;
        double nativeTerrainMs = _profileNativeTerrainPassTicks * inv / _profileFrameCount;
        double auxMs = _profileAuxUploadTicks * inv / _profileFrameCount;
        double postMs = _profilePostPassTicks * inv / _profileFrameCount;
        double postSetupMs = _profilePostSetupTicks * inv / _profileFrameCount;
        double postCbMs = _profilePostCbUpdateTicks * inv / _profileFrameCount;
        double postDrawMs = _profilePostDrawTicks * inv / _profileFrameCount;
        double blitMs = _profileFinalBlitTicks * inv / _profileFrameCount;
        double uiMs = _profileUiRenderTicks * inv / _profileFrameCount;
        Console.WriteLine(
            $"[DX11 Profile] sceneUpload={sceneMs:F3}ms nativeTerrain={nativeTerrainMs:F3}ms auxUpload={auxMs:F3}ms postTotal={postMs:F3}ms " +
            $"postSetup={postSetupMs:F3}ms postCb={postCbMs:F3}ms postDraw={postDrawMs:F3}ms blit={blitMs:F3}ms ui={uiMs:F3}ms");
        _profileSceneUploadTicks = 0;
        _profileNativeTerrainPassTicks = 0;
        _profileAuxUploadTicks = 0;
        _profilePostPassTicks = 0;
        _profilePostSetupTicks = 0;
        _profilePostCbUpdateTicks = 0;
        _profilePostDrawTicks = 0;
        _profileFinalBlitTicks = 0;
        _profileUiRenderTicks = 0;
        _profileFrameCount = 0;
    }
}
