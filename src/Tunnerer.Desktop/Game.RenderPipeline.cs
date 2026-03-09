namespace Tunnerer.Desktop;

using Tunnerer.Core.Config;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;
using Tunnerer.Desktop.Gui;
using Tunnerer.Desktop.Input;
using Tunnerer.Desktop.Rendering;
using System.Diagnostics;

public partial class Game
{
    private void RenderImGuiFrame(IReadOnlyList<Core.Entities.Tank> tanks, in FrameInputSnapshot frameInput)
    {
        var (winW, winH) = _renderer.GetWindowSize();
        float dt = 1f / Tweaks.Perf.TargetFps;
        int scale = DesktopScreenTweaks.PixelScale;
        var viewSize = _renderViewState.ComputeViewSize(winW, winH, _renderBackend.SupportsUi);
        _renderViewState.EnsureHiResBuffer(viewSize);

        var player = tanks.Count > 0 ? tanks[0] : null;
        _renderViewState.UpdateCamera(player, _terrainSize, viewSize.X, viewSize.Y, scale);
        var renderView = new RenderView(
            ViewSize: _renderViewState.HiResSize,
            WorldSize: _terrainSize,
            CameraPixels: new Position(_renderViewState.CamPixelX, _renderViewState.CamPixelY),
            PixelScale: scale);

        ProfileSection(ref _drawProfile.ScreenDraw, () =>
        {
            var hiResWatch = Stopwatch.StartNew();

            var visibleRect = _renderViewState.ComputeVisibleWorldRect(_terrainSize, scale);

            Rect? auxDirtyRect;
            bool consumeHeatDirty;
            if (_gpuAuxFullUploadPending)
            {
                ProfileSection(ref _drawProfile.ScreenAuxBuild, () =>
                    _terrainAuxBuilder.BuildGpuTerrainAuxData(_world.Terrain, _gpuTerrainAux));
                auxDirtyRect = new Rect(0, 0, _terrainSize.X, _terrainSize.Y);
                consumeHeatDirty = true;
            }
            else
            {
                Rect? terrainDirtyRect = RenderViewState.ClampToViewport(
                    RenderViewState.TryGetDirtyCellBounds(_terrainDirtyCells), visibleRect);
                bool includeHeatAux = (_heatAuxFrameCounter++ % DesktopScreenTweaks.HeatAuxUpdateIntervalFrames) == 0;
                Rect? heatDirtyRect = includeHeatAux && _world.Terrain.TryGetHeatDirtyRect(out Rect dirtyRect)
                    ? RenderViewState.ClampToViewport(dirtyRect, visibleRect)
                    : null;

                Rect? cameraRevealRect = _renderViewState.ComputeCameraRevealRect(visibleRect);

                // Terrain-change and camera-reveal need full 4-channel aux rebuild
                // (R=heat, G=SDF, B=material class, A=scorch level).
                Rect? fullAuxRect = RenderViewState.MergeDirtyRects(terrainDirtyRect, cameraRevealRect);

                if (fullAuxRect is Rect fullRect)
                    ProfileSection(ref _drawProfile.AuxTerrainPack, () =>
                        _terrainAuxBuilder.BuildGpuTerrainAuxRect(_world.Terrain, _gpuTerrainAux, fullRect));

                // Heat-only R-channel fast path for the remaining visible heat area
                Rect? heatOnlyRect = heatDirtyRect;
                if (heatOnlyRect is not null && fullAuxRect is not null)
                    heatOnlyRect = RenderViewState.SubtractCoveredHeatRect(heatOnlyRect, fullAuxRect);

                if (heatOnlyRect is Rect heatRect)
                    ProfileSection(ref _drawProfile.AuxHeatPack, () =>
                        TerrainAuxBuilder.UpdateGpuTerrainAuxHeatOnly(_world.Terrain, _gpuTerrainAux, heatRect));

                auxDirtyRect = RenderViewState.MergeDirtyRects(fullAuxRect, heatOnlyRect);
                consumeHeatDirty = includeHeatAux;
            }
            _renderViewState.UpdateLastAuxViewport(visibleRect);
            _terrainDirtyCells.Clear();

            _drawProfile.ScreenHiResTerrain += hiResWatch.Elapsed;
            double terrainMs = hiResWatch.Elapsed.TotalMilliseconds;

            hiResWatch.Restart();
            int tankHeatGlowCount = 0;
            ProfileSection(ref _drawProfile.ScreenTankGlowBuild, () =>
                tankHeatGlowCount = _terrainAuxBuilder.BuildGpuTankHeatGlowData(
                    _world.TankList.Tanks, renderView, _gpuTankHeatGlow));

            _drawProfile.ScreenHiResEntities += hiResWatch.Elapsed;

            hiResWatch.Restart();
            var upload = new GamePixelsUpload(
                Pixels: Array.Empty<uint>(),
                View: renderView,
                PostProcess: new PostProcessUploadOptions(
                    Quality: HiResRenderQuality.High,
                    HeatDebugOverlayEnabled: _commandController.ShowHeatDebugOverlay,
                    PassFlags: _commandController.EnabledPostPasses,
                    ThermalRegions: BuildThermalRegionOverlayUpload()),
                TankGlow: new TankGlowUpload(
                    Data: _gpuTankHeatGlow,
                    Count: tankHeatGlowCount),
                TerrainAux: new TerrainAuxUpload(
                    Data: _gpuTerrainAux,
                    DirtyRect: auxDirtyRect),
                NativeContinuous: new NativeContinuousUpload(
                    Enabled: true,
                    SourcePixels: _compositePixels,
                    SampleCount: _renderViewState.NativeContinuousSampleCount));
            ProfileSection(ref _drawProfile.ScreenBackendUpload, () =>
                _renderBackend.UploadGamePixels(upload));
            if (consumeHeatDirty)
                _world.Terrain.ClearHeatDirtyRect();
            _gpuAuxFullUploadPending = false;
            _drawProfile.ScreenUpload += hiResWatch.Elapsed;
            double uploadMs = hiResWatch.Elapsed.TotalMilliseconds;

            ProfileSection(ref _drawProfile.ScreenQualityAdjust, () =>
            {
                _renderViewState.UpdateNativeContinuousQuality(terrainMs + uploadMs);
            });
        });

        ProfileSection(ref _drawProfile.ScreenClearFrame, () =>
            _renderBackend.ClearFrame(new Size(winW, winH), new Tunnerer.Core.Types.Color(26, 26, 26, 255)));

        var imguiWatch = Stopwatch.StartNew();
        if (_renderBackend.SupportsUi)
        {
            ProfileSection(ref _drawProfile.ScreenNewFrame, () =>
                _renderBackend.NewFrame(winW, winH, dt));

            if (player != null)
            {
                var vp = _hud.ViewportRect;
                if (vp.w > 0 && vp.h > 0 &&
                    frameInput.MouseX >= vp.x && frameInput.MouseY >= vp.y &&
                    frameInput.MouseX < vp.x + vp.w && frameInput.MouseY < vp.y + vp.h)
                {
                    _hud.CrosshairScreenPos = (frameInput.MouseX, frameInput.MouseY);
                }
                else
                    _hud.CrosshairScreenPos = null;

                var gameTextureSize = _renderBackend.GameTextureSize;
                ProfileSection(ref _drawProfile.ScreenHudDraw, () =>
                    _hud.Draw(
                        _renderBackend.GameTextureId,
                        gameTextureSize.X,
                        gameTextureSize.Y,
                        player,
                        _world,
                        dt,
                        _commandController.ShowHeatDebugOverlay,
                        _commandController.EnabledPostPasses,
                        _commandController.ShowPostPassOverlay));
            }

        }

        _drawProfile.ScreenUi += imguiWatch.Elapsed;
        imguiWatch.Restart();
        _renderBackend.Render();
        _drawProfile.ScreenImGuiRender += imguiWatch.Elapsed;

        imguiWatch.Restart();
        _renderer.SwapWindow();
        _drawProfile.ScreenSwap += imguiWatch.Elapsed;
    }

    private ThermalRegionOverlayUpload BuildThermalRegionOverlayUpload()
    {
        if (!_world.Terrain.TryGetThermalTileInfo(out int tileSize, out _, out _))
            return default;

        float threshold01 = _world.Settings.ThermalActiveTemperatureThreshold / (TerrainGrid.HeatByteScale * 255f);
        return new ThermalRegionOverlayUpload(
            TileSizeCells: Math.Max(1, tileSize),
            ActiveThresholdHeat01: Math.Clamp(threshold01, 0f, 1f));
    }
}
