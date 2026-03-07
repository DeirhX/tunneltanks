namespace Tunnerer.Desktop;

using Tunnerer.Core.Config;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;
using Tunnerer.Desktop.Gui;
using Tunnerer.Desktop.Rendering;
using System.Diagnostics;

public partial class Game
{
    private void RenderImGuiFrame(IReadOnlyList<Core.Entities.Tank> tanks)
    {
        var (winW, winH) = _renderer.GetWindowSize();
        float dt = 1f / Tweaks.Perf.TargetFps;
        int scale = DesktopScreenTweaks.PixelScale;
        int viewportHeight = _renderBackend.SupportsUi
            ? Math.Max(1, winH - (int)GameHud.BottomPanelHeight)
            : Math.Max(1, winH);
        var viewSize = new Size(Math.Max(1, winW), viewportHeight);
        EnsureHiResBuffer(viewSize);

        var player = tanks.Count > 0 ? tanks[0] : null;
        UpdateCamera(player, viewSize.X, viewSize.Y, scale);
        var renderView = new RenderView(
            ViewSize: _hiResSize,
            WorldSize: _terrainSize,
            CameraPixels: new Position(_camPixelX, _camPixelY),
            PixelScale: scale);

        ProfileSection(ref _drawProfile.ScreenDraw, () =>
        {
            var hiResWatch = Stopwatch.StartNew();

            var visibleRect = ComputeVisibleWorldRect();

            Rect? auxDirtyRect;
            bool consumeHeatDirty;
            if (_gpuAuxFullUploadPending)
            {
                ProfileSection(ref _drawProfile.ScreenAuxBuild, () =>
                    BuildGpuTerrainAuxData(_world.Terrain, _gpuTerrainAux));
                auxDirtyRect = new Rect(0, 0, _terrainSize.X, _terrainSize.Y);
                consumeHeatDirty = true;
            }
            else
            {
                Rect? terrainDirtyRect = ClampToViewport(TryGetDirtyCellBounds(_terrainDirtyCells), visibleRect);
                bool includeHeatAux = (_heatAuxFrameCounter++ % DesktopScreenTweaks.HeatAuxUpdateIntervalFrames) == 0;
                Rect? heatDirtyRect = includeHeatAux && _world.Terrain.TryGetHeatDirtyRect(out Rect dirtyRect)
                    ? ClampToViewport(dirtyRect, visibleRect)
                    : null;

                Rect? cameraRevealRect = ComputeCameraRevealRect(visibleRect);

                // Terrain-change and camera-reveal need full 4-channel aux rebuild
                // (R=heat, G=SDF, B=material class, A=scorch level).
                Rect? fullAuxRect = MergeDirtyRects(terrainDirtyRect, cameraRevealRect);

                if (fullAuxRect is Rect fullRect)
                    ProfileSection(ref _drawProfile.AuxTerrainPack, () =>
                        BuildGpuTerrainAuxRect(_world.Terrain, _gpuTerrainAux, fullRect));

                // Heat-only R-channel fast path for the remaining visible heat area
                Rect? heatOnlyRect = heatDirtyRect;
                if (heatOnlyRect is not null && fullAuxRect is not null)
                    heatOnlyRect = SubtractCoveredHeatRect(heatOnlyRect, fullAuxRect);

                if (heatOnlyRect is Rect heatRect)
                    ProfileSection(ref _drawProfile.AuxHeatPack, () =>
                        UpdateGpuTerrainAuxHeatOnly(_world.Terrain, _gpuTerrainAux, heatRect));

                auxDirtyRect = MergeDirtyRects(fullAuxRect, heatOnlyRect);
                consumeHeatDirty = includeHeatAux;
            }
            _lastAuxViewport = visibleRect;
            _terrainDirtyCells.Clear();

            _drawProfile.ScreenHiResTerrain += hiResWatch.Elapsed;
            double terrainMs = hiResWatch.Elapsed.TotalMilliseconds;

            hiResWatch.Restart();
            int tankHeatGlowCount = 0;
            ProfileSection(ref _drawProfile.ScreenTankGlowBuild, () =>
                tankHeatGlowCount = BuildGpuTankHeatGlowData(
                    _world.TankList.Tanks, renderView));

            _drawProfile.ScreenHiResEntities += hiResWatch.Elapsed;

            hiResWatch.Restart();
            var upload = new GamePixelsUpload(
                Pixels: Array.Empty<uint>(),
                View: renderView,
                PostProcess: new PostProcessUploadOptions(
                    Quality: HiResRenderQuality.High,
                    HeatDebugOverlayEnabled: _showHeatDebugOverlay,
                    PassFlags: _enabledPostPasses),
                TankGlow: new TankGlowUpload(
                    Data: _gpuTankHeatGlow,
                    Count: tankHeatGlowCount),
                TerrainAux: new TerrainAuxUpload(
                    Data: _gpuTerrainAux,
                    DirtyRect: auxDirtyRect),
                NativeContinuous: new NativeContinuousUpload(
                    Enabled: true,
                    SourcePixels: _compositePixels,
                    SampleCount: _nativeContinuousSampleCount));
            ProfileSection(ref _drawProfile.ScreenBackendUpload, () =>
                _renderBackend.UploadGamePixels(upload));
            if (consumeHeatDirty)
                _world.Terrain.ClearHeatDirtyRect();
            _gpuAuxFullUploadPending = false;
            _drawProfile.ScreenUpload += hiResWatch.Elapsed;
            double uploadMs = hiResWatch.Elapsed.TotalMilliseconds;

            ProfileSection(ref _drawProfile.ScreenQualityAdjust, () =>
            {
                UpdateNativeContinuousQuality(terrainMs + uploadMs);
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
                var (mx, my, _) = _renderer.GetMouseState();
                var vp = _hud.ViewportRect;
                if (vp.w > 0 && vp.h > 0 &&
                    mx >= vp.x && my >= vp.y && mx < vp.x + vp.w && my < vp.y + vp.h)
                    _hud.CrosshairScreenPos = (mx, my);
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
                        _enabledPostPasses,
                        _showPostPassOverlay));
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

    private static Rect? SubtractCoveredHeatRect(Rect? heat, Rect? full)
    {
        if (heat is null || full is null) return heat;
        var h = heat.Value;
        var f = full.Value;
        if (h.Left >= f.Left && h.Top >= f.Top && h.Right <= f.Right && h.Bottom <= f.Bottom)
            return null;
        return heat;
    }
}
