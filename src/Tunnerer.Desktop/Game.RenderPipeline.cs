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
            _ = (float)_gameTimer.Elapsed.TotalSeconds;

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
                Rect? terrainDirtyRect = TryGetDirtyCellBounds(_terrainDirtyCells);
                bool includeHeatAux = (_heatAuxFrameCounter++ % DesktopScreenTweaks.HeatAuxUpdateIntervalFrames) == 0;
                Rect? heatDirtyRect = includeHeatAux && _world.Terrain.TryGetHeatDirtyRect(out Rect dirtyRect) ? dirtyRect : null;
                auxDirtyRect = MergeDirtyRects(terrainDirtyRect, heatDirtyRect);
                consumeHeatDirty = includeHeatAux;
                if (auxDirtyRect is Rect rect)
                    ProfileSection(ref _drawProfile.ScreenAuxBuild, () =>
                        BuildGpuTerrainAuxRect(_world.Terrain, _gpuTerrainAux, rect));
            }
            _terrainDirtyCells.Clear();

            // NativeContinuous path uploads world-resolution composite pixels directly to GPU.
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
                Quality: HiResRenderQuality.High,
                TankHeatGlowData: _gpuTankHeatGlow,
                TankHeatGlowCount: tankHeatGlowCount,
                TerrainAux: _gpuTerrainAux,
                AuxDirtyRect: auxDirtyRect,
                UseNativeContinuous: true,
                NativeSourcePixels: _compositePixels,
                NativeSampleCount: _nativeContinuousSampleCount);
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
                    _hud.Draw(_renderBackend.GameTextureId, gameTextureSize.X, gameTextureSize.Y, player, _world, dt));
            }

            _drawProfile.ScreenUi += imguiWatch.Elapsed;

            imguiWatch.Restart();
            _renderBackend.Render();
            _drawProfile.ScreenImGuiRender += imguiWatch.Elapsed;
        }
        else
        {
            _drawProfile.ScreenUi += imguiWatch.Elapsed;
            imguiWatch.Restart();
            _renderBackend.Render();
            _drawProfile.ScreenImGuiRender += imguiWatch.Elapsed;
        }

        imguiWatch.Restart();
        _renderer.SwapWindow();
        _drawProfile.ScreenSwap += imguiWatch.Elapsed;
    }
}
