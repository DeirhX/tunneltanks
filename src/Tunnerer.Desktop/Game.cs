namespace Tunnerer.Desktop;

using Silk.NET.SDL;
using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Input;
using Tunnerer.Core.LevelGen;
using Tunnerer.Core.Types;
using Surface = Tunnerer.Core.Types.Surface;
using Tunnerer.Desktop.Gui;
using Tunnerer.Desktop.Input;
using Tunnerer.Desktop.Rendering;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

public class Game : IDisposable
{
    private readonly SdlRenderer _renderer;
    private readonly IGameRenderBackend _renderBackend;
    private readonly ITextureLoader _textures;
    private readonly GameHud _hud;

    private readonly Size _terrainSize;
    private readonly World _world;
    private readonly uint[] _worldPixels;
    private readonly uint[] _compositePixels;
    private readonly KeyboardController _p1Controller;
    private readonly BotTankAI _p2AI;
    private readonly HiResTerrainRenderer _hiResTerrainRenderer = new();
    private readonly HiResEntityRenderer _hiResEntityRenderer = new();
    private readonly List<Position> _terrainDirtyCells = new();
    private readonly float[] _gpuTankHeatGlow = new float[Tweaks.World.MaxPlayers * 4];
    private readonly byte[] _gpuTerrainAux;
    private readonly TerrainBlurField _gpuBlurField = new();
    private bool _gpuAuxFullUploadPending = true;
    private uint[] _hiResPixels = Array.Empty<uint>();
    private uint[] _hiResTerrainPixels = Array.Empty<uint>();
    private Size _hiResSize;
    private bool _hiResTerrainNeedsFullRender = true;
    private HiResRenderQuality _hiResQuality =
        (HiResRenderQuality)Math.Clamp(Tweaks.Screen.HiResInitialQuality, 0, 2);
    private readonly TerrainVisualMode _terrainVisualMode;
    private int _nativeContinuousSampleCount = Tweaks.Screen.NativeContinuousSampleHigh;
    private int _nativeOverBudgetFrames;
    private int _nativeUnderBudgetFrames;
    private int _overBudgetFrames;
    private int _underBudgetFrames;
    private readonly Stopwatch _gameTimer = Stopwatch.StartNew();

    private int _camPixelX;
    private int _camPixelY;
    private int _prevCamPixelX = -1;
    private int _prevCamPixelY = -1;

    public const int DefaultSeed = 42;

    private readonly DrawProfile _drawProfile = new();
    private readonly PerfCaptureOptions? _perfCapture;
    private readonly List<double> _perfFrameMs = new();
    private int _perfFramesSeen;
    private bool _isRunning = true;

    public unsafe Game(
        Size? terrainSizeOverride = null,
        LevelGenMode genMode = LevelGenMode.Deterministic,
        PerfCaptureOptions? perfCapture = null,
        RenderBackendKind? renderBackendOverride = null)
    {
        var windowSize = Tweaks.Screen.WindowSize;
        _terrainSize = terrainSizeOverride ?? Tweaks.Screen.RenderSurfaceSize;
        RenderBackendKind selectedBackend = renderBackendOverride ?? Tweaks.System.RenderBackend;
        _terrainVisualMode = ResolveTerrainVisualMode();
        bool parallel = genMode == LevelGenMode.Optimized;
        if (perfCapture is PerfCaptureOptions perf)
        {
            _perfCapture = new PerfCaptureOptions(
                Math.Max(0, perf.WarmupFrames),
                Math.Max(1, perf.MeasureFrames),
                string.IsNullOrWhiteSpace(perf.CsvPath) ? null : perf.CsvPath);
            _perfFrameMs = new List<double>(_perfCapture.Value.MeasureFrames);
            Console.WriteLine($"[Perf] enabled warmup={_perfCapture.Value.WarmupFrames} measure={_perfCapture.Value.MeasureFrames}");
        }

        var graphicsMode = selectedBackend == RenderBackendKind.OpenGl
            ? SdlGraphicsMode.OpenGl
            : SdlGraphicsMode.NativeWindow;
        _renderer = new SdlRenderer(Tweaks.System.WindowTitle, windowSize, graphicsMode);

        var renderServices = RenderBackendFactory.CreateServices(
            selectedBackend, _renderer.Sdl, _renderer.NativeWindow, windowSize.X, windowSize.Y);
        _renderBackend = renderServices.Backend;
        _textures = renderServices.Textures;
        Console.WriteLine($"[Render] Backend={selectedBackend} SDLMode={graphicsMode}");
        _hud = new GameHud();
        LoadHudSprites();

        var modeLabel = parallel ? "optimized" : "deterministic";
        Console.WriteLine($"Generating terrain {_terrainSize.X}x{_terrainSize.Y} ({modeLabel}, seed={DefaultSeed})...");
        var genWatch = Stopwatch.StartNew();
        var generator = new ToastGenerator();
        int? seed = parallel ? null : DefaultSeed;
        var (terrain, spawns) = generator.Generate(_terrainSize, seed, genMode);
        Console.WriteLine($"Level generated in {genWatch.Elapsed.TotalMilliseconds:F0} ms, {spawns.Length} spawns");

        _world = new World(_terrainSize);
        int? matSeed = parallel ? null : DefaultSeed + 1;
        _world.Initialize(terrain, spawns, materializeSeed: matSeed, parallelMaterialize: parallel);
        _p2AI = new BotTankAI(seed: parallel ? null : DefaultSeed + 2);

        _worldPixels = new uint[_terrainSize.Area];
        _compositePixels = new uint[_terrainSize.Area];
        _gpuTerrainAux = new byte[_terrainSize.Area * 4];
        if (parallel)
            _world.Terrain.DrawAllToSurfaceParallel(_worldPixels);
        else
            _world.Terrain.DrawAllToSurface(_worldPixels);

        _p1Controller = new KeyboardController(_renderer.Sdl, new KeyBindings(
            Left: Scancode.ScancodeA, Right: Scancode.ScancodeD,
            Up: Scancode.ScancodeW, Down: Scancode.ScancodeS,
            Shoot: Scancode.ScancodeSpace));
        Console.WriteLine($"[Render] TerrainVisual={_terrainVisualMode}");
    }

    public void Run()
    {
        try
        {
            var frameTimer = Stopwatch.StartNew();
            var targetFrameTime = Tweaks.World.AdvanceStep;
            var tanks = _world.TankList.Tanks;

            while (_isRunning)
            {
                if (!_renderer.PollEvents(ev => _renderBackend.ProcessEvent(ev)))
                { _isRunning = false; break; }

                if (_world.IsGameOver)
                { _isRunning = false; break; }

                if (frameTimer.Elapsed >= targetFrameTime)
                {
                    frameTimer.Restart();
                    var totalFrameWatch = Stopwatch.StartNew();

                    var aimDir = ComputeAimDirection(tanks);

                    bool mouseShoot = IsMouseInViewport(out _, out _) && IsLeftMouseDown();

                    _world.Advance(i =>
                    {
                        if (i == 0)
                        {
                            var kb = _p1Controller.Poll();
                            return new ControllerOutput
                            {
                                MoveSpeed = kb.MoveSpeed,
                                ShootPrimary = kb.ShootPrimary || mouseShoot,
                                AimDirection = aimDir ?? default,
                            };
                        }
                        var enemy = tanks.Count > 0 ? tanks[0] : null;
                        return _p2AI.GetInput(_world.TankList.Tanks[i], enemy, _world.Terrain);
                    });

                    _terrainDirtyCells.Clear();
                    var changedCells = _world.Terrain.GetChangeList();
                    if (changedCells.Count > 0)
                        _terrainDirtyCells.AddRange(changedCells);

                    ProfileSection(ref _drawProfile.TerrainDraw,
                        () => _world.Terrain.DrawChangesToSurface(_worldPixels));

                    ProfileSection(ref _drawProfile.ObjectsDraw, () =>
                    {
                        Array.Copy(_worldPixels, _compositePixels, _worldPixels.Length);
                        var compositeSurface = new Surface(_compositePixels, _terrainSize.X, _terrainSize.Y);
                        _world.LinkMap.Draw(compositeSurface);
                        _world.Machines.Draw(compositeSurface);
                        _world.Projectiles.Draw(compositeSurface);
                        _world.Sprites.Draw(compositeSurface);
                        _world.TankList.Draw(compositeSurface);

                        MarkEntityPixels(_worldPixels, _compositePixels);
                    });

                    RenderImGuiFrame(tanks);

                    _drawProfile.TotalFrame += totalFrameWatch.Elapsed;
                    _drawProfile.FrameCount++;
                    if (_drawProfile.FrameCount >= 100)
                        _drawProfile.Report();

                    if (CapturePerfFrame(totalFrameWatch.Elapsed))
                        _isRunning = false;
                }
            }

            ReportPerfCaptureIfEnabled();
        }
        finally
        {
            _textures.Dispose();
            _renderBackend.Dispose();
            _renderer.Dispose();
        }
    }

    private bool CapturePerfFrame(TimeSpan totalFrame)
    {
        if (_perfCapture is not PerfCaptureOptions perf)
            return false;

        _perfFramesSeen++;
        if (_perfFramesSeen > perf.WarmupFrames)
            _perfFrameMs.Add(totalFrame.TotalMilliseconds);

        return _perfFrameMs.Count >= perf.MeasureFrames;
    }

    private void ReportPerfCaptureIfEnabled()
    {
        if (_perfCapture is not PerfCaptureOptions perf || _perfFrameMs.Count == 0)
            return;

        double sum = 0;
        for (int i = 0; i < _perfFrameMs.Count; i++)
            sum += _perfFrameMs[i];

        double[] sorted = _perfFrameMs.ToArray();
        Array.Sort(sorted);
        double avg = sum / _perfFrameMs.Count;
        double min = sorted[0];
        double max = sorted[^1];
        double p50 = Percentile(sorted, 0.50);
        double p95 = Percentile(sorted, 0.95);
        double p99 = Percentile(sorted, 0.99);

        Console.WriteLine(
            $"[Perf] frames={_perfFrameMs.Count} warmup={perf.WarmupFrames} avg={avg:F3}ms " +
            $"p50={p50:F3}ms p95={p95:F3}ms p99={p99:F3}ms min={min:F3}ms max={max:F3}ms");

        if (perf.CsvPath is null)
            return;

        string fullPath = Path.GetFullPath(perf.CsvPath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(fullPath, false);
        writer.WriteLine("frame,ms");
        for (int i = 0; i < _perfFrameMs.Count; i++)
            writer.WriteLine($"{i},{_perfFrameMs[i].ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"[Perf] wrote csv: {fullPath}");
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        int idx = (int)Math.Ceiling(sorted.Length * p) - 1;
        idx = Math.Clamp(idx, 0, sorted.Length - 1);
        return sorted[idx];
    }

    private void RenderImGuiFrame(IReadOnlyList<Core.Entities.Tank> tanks)
    {
        var (winW, winH) = _renderer.GetWindowSize();
        float dt = 1f / Tweaks.Perf.TargetFps;
        int scale = Tweaks.Screen.PixelScale;
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

        int dx = _camPixelX - _prevCamPixelX;
        int dy = _camPixelY - _prevCamPixelY;
        bool cameraMoved = dx != 0 || dy != 0;
        _prevCamPixelX = _camPixelX;
        _prevCamPixelY = _camPixelY;

        ProfileSection(ref _drawProfile.ScreenDraw, () =>
        {
            var hiResWatch = Stopwatch.StartNew();
            float renderTime = (float)_gameTimer.Elapsed.TotalSeconds;
            bool useNativeContinuous = _terrainVisualMode == TerrainVisualMode.NativeContinuous;

            if (!useNativeContinuous)
            {
                if (_hiResTerrainNeedsFullRender ||
                    Math.Abs(dx) >= viewSize.X || Math.Abs(dy) >= viewSize.Y)
                {
                    ProfileSection(ref _drawProfile.ScreenTerrainFullRender, () =>
                        _hiResTerrainRenderer.Render(_world.Terrain, _hiResTerrainPixels,
                            renderView, _hiResQuality, renderTime));
                    _hiResTerrainNeedsFullRender = false;
                }
                else if (cameraMoved)
                {
                    var cameraDelta = new Offset(dx, dy);
                    ProfileSection(ref _drawProfile.ScreenTerrainScrollCopy, () =>
                        ScrollTerrainBuffer(cameraDelta, viewSize));
                    ProfileSection(ref _drawProfile.ScreenTerrainExposedStrips, () =>
                        RenderExposedStrips(_hiResTerrainPixels, renderView, cameraDelta));
                }

                if (_terrainDirtyCells.Count > 0)
                {
                    ProfileSection(ref _drawProfile.ScreenTerrainDirtyRender, () =>
                        _hiResTerrainRenderer.RenderDirty(_world.Terrain, _hiResTerrainPixels,
                            renderView, _hiResQuality,
                            _terrainDirtyCells, renderTime));
                }
            }

            Rect? auxDirtyRect;
            if (_gpuAuxFullUploadPending)
            {
                ProfileSection(ref _drawProfile.ScreenAuxBuild, () =>
                    BuildGpuTerrainAuxData(_world.Terrain, _gpuTerrainAux));
                auxDirtyRect = new Rect(0, 0, _terrainSize.X, _terrainSize.Y);
            }
            else
            {
                Rect? terrainDirtyRect = TryGetDirtyCellBounds(_terrainDirtyCells);
                Rect? heatDirtyRect = _world.Terrain.TryGetHeatDirtyRect(out Rect dirtyRect) ? dirtyRect : null;
                auxDirtyRect = MergeDirtyRects(terrainDirtyRect, heatDirtyRect);
                if (auxDirtyRect is Rect rect)
                    ProfileSection(ref _drawProfile.ScreenAuxBuild, () =>
                        BuildGpuTerrainAuxRect(_world.Terrain, _gpuTerrainAux, rect));
            }
            _terrainDirtyCells.Clear();

            if (useNativeContinuous)
            {
                // NativeContinuous path uploads world-resolution composite pixels directly to GPU.
            }
            else
            {
                ProfileSection(ref _drawProfile.ScreenTerrainToSceneCopy, () =>
                    Array.Copy(_hiResTerrainPixels, _hiResPixels, _hiResPixels.Length));
            }
            _drawProfile.ScreenHiResTerrain += hiResWatch.Elapsed;
            double terrainMs = hiResWatch.Elapsed.TotalMilliseconds;

            hiResWatch.Restart();
            if (!useNativeContinuous)
            {
                ProfileSection(ref _drawProfile.ScreenEntityRender, () =>
                    _hiResEntityRenderer.Render(
                        _hiResPixels, renderView,
                        _worldPixels, _compositePixels,
                        renderTime));
            }

            int tankHeatGlowCount = 0;
            ProfileSection(ref _drawProfile.ScreenTankGlowBuild, () =>
                tankHeatGlowCount = BuildGpuTankHeatGlowData(
                    _world.TankList.Tanks, renderView));

            _drawProfile.ScreenHiResEntities += hiResWatch.Elapsed;
            double entityMs = hiResWatch.Elapsed.TotalMilliseconds;

            hiResWatch.Restart();
            var upload = new GamePixelsUpload(
                Pixels: _hiResPixels,
                View: renderView,
                Quality: _hiResQuality,
                TankHeatGlowData: _gpuTankHeatGlow,
                TankHeatGlowCount: tankHeatGlowCount,
                TerrainAux: _gpuTerrainAux,
                AuxDirtyRect: auxDirtyRect,
                UseNativeContinuous: useNativeContinuous,
                NativeSourcePixels: useNativeContinuous ? _compositePixels : null,
                NativeSampleCount: _nativeContinuousSampleCount);
            ProfileSection(ref _drawProfile.ScreenBackendUpload, () =>
                _renderBackend.UploadGamePixels(upload));
            _world.Terrain.ClearHeatDirtyRect();
            _gpuAuxFullUploadPending = false;
            _drawProfile.ScreenUpload += hiResWatch.Elapsed;
            double uploadMs = hiResWatch.Elapsed.TotalMilliseconds;

            ProfileSection(ref _drawProfile.ScreenQualityAdjust, () =>
            {
                if (useNativeContinuous)
                    UpdateNativeContinuousQuality(terrainMs + uploadMs);
                else
                    UpdateHiResQuality(terrainMs + entityMs + uploadMs);
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

                ProfileSection(ref _drawProfile.ScreenHudDraw, () =>
                    _hud.Draw(_renderBackend.GameTextureId, _hiResSize.X, _hiResSize.Y, player, _world, dt));
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

    private void UpdateCamera(Core.Entities.Tank? player, int viewW, int viewH, int scale)
    {
        int worldPixelW = _terrainSize.X * scale;
        int worldPixelH = _terrainSize.Y * scale;

        if (player != null)
        {
            int centerPx = (int)MathF.Round(player.Position.X * scale);
            int centerPy = (int)MathF.Round(player.Position.Y * scale);
            _camPixelX = centerPx - viewW / 2;
            _camPixelY = centerPy - viewH / 2;
        }

        if (viewW >= worldPixelW)
            _camPixelX = -(viewW - worldPixelW) / 2;
        else
            _camPixelX = Math.Clamp(_camPixelX, 0, worldPixelW - viewW);

        if (viewH >= worldPixelH)
            _camPixelY = -(viewH - worldPixelH) / 2;
        else
            _camPixelY = Math.Clamp(_camPixelY, 0, worldPixelH - viewH);
    }

    private int BuildGpuTankHeatGlowData(
        IReadOnlyList<Core.Entities.Tank> tanks,
        in RenderView view)
    {
        int camPixelX = view.CameraPixels.X;
        int camPixelY = view.CameraPixels.Y;
        int pixelScale = view.PixelScale;
        int targetW = view.ViewSize.X;
        int targetH = view.ViewSize.Y;
        int count = 0;
        for (int i = 0; i < tanks.Count && count < Tweaks.World.MaxPlayers; i++)
        {
            var tank = tanks[i];
            if (tank.IsDead) continue;

            float t = tank.Heat / Tweaks.Tank.HeatMax;
            float minVisible = Tweaks.Screen.PostTankHeatGlowMinHeat / Tweaks.Tank.HeatMax;
            float damageStart = Tweaks.Tank.HeatSafeMax / Tweaks.Tank.HeatMax;
            float visibleRange = Math.Max(0.0001f, damageStart - minVisible);
            // Visual heat reaches peak around overheat onset, so damage state already looks max-hot.
            float visibleT = Math.Clamp((t - minVisible) / visibleRange, 0.0f, 1.0f);
            if (tank.Reactor.Health < tank.Reactor.HealthCapacity)
            {
                // Health damage should already look close to max-hot for clear player feedback.
                float damageFrac = 1.0f - (float)tank.Reactor.Health / Math.Max(1f, tank.Reactor.HealthCapacity);
                float damageVisual = 0.85f + 0.15f * MathF.Sqrt(Math.Clamp(damageFrac * 8.0f, 0.0f, 1.0f));
                visibleT = Math.Max(visibleT, damageVisual);
            }
            if (visibleT <= 0.0f) continue;

            // Keep visibility from lower heat while giving max heat a strong punch.
            float intensity = visibleT * (0.35f + 2.8f * visibleT * visibleT);
            float radiusFactor = 0.45f + 0.55f * MathF.Sqrt(visibleT);
            float glowRadiusPx = pixelScale * (Tweaks.Screen.PostTankHeatGlowBaseRadius + Tweaks.Screen.PostTankHeatGlowScaleRadius * radiusFactor);
            float cx = (tank.Position.X + 0.5f) * pixelScale - camPixelX;
            float cy = (tank.Position.Y + 0.5f) * pixelScale - camPixelY;

            if (cx + glowRadiusPx < 0 || cy + glowRadiusPx < 0 || cx - glowRadiusPx >= targetW || cy - glowRadiusPx >= targetH)
                continue;

            int baseIdx = count * 4;
            _gpuTankHeatGlow[baseIdx + 0] = cx / targetW;
            _gpuTankHeatGlow[baseIdx + 1] = cy / targetH;
            _gpuTankHeatGlow[baseIdx + 2] = glowRadiusPx / MathF.Max(targetW, targetH);
            _gpuTankHeatGlow[baseIdx + 3] = intensity;
            count++;
        }

        return count;
    }

    private static Rect? TryGetDirtyCellBounds(IReadOnlyList<Position> dirtyCells)
    {
        if (dirtyCells.Count == 0)
            return null;

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            var p = dirtyCells[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return RectMath.FromMinMaxInclusive(minX, minY, maxX, maxY);
    }

    private static Rect? MergeDirtyRects(Rect? a, Rect? b)
    {
        return RectMath.Union(a, b);
    }

    private void BuildGpuTerrainAuxData(Core.Terrain.TerrainGrid terrain, byte[] target)
    {
        _gpuBlurField.Rebuild(terrain);
        int w = terrain.Width;
        int len = terrain.Size.Area;
        for (int i = 0; i < len; i++)
        {
            int x = i % w, y = i / w;
            WriteTerrainAux(terrain, i, terrain.GetPixelRaw(i), _gpuBlurField.SampleAsByte(x, y), target, i * 4);
        }
    }

    private void BuildGpuTerrainAuxRect(Core.Terrain.TerrainGrid terrain, byte[] target, in Rect dirtyRect)
    {
        RectMath.GetMinMaxInclusive(dirtyRect, out int minX, out int minY, out int maxX, out int maxY);
        _gpuBlurField.UpdateRect(terrain, minX, minY, maxX, maxY);
        int w = terrain.Width;
        for (int y = minY; y <= maxY; y++)
        {
            int row = y * w;
            for (int x = minX; x <= maxX; x++)
            {
                int idx = row + x;
                WriteTerrainAux(terrain, idx, terrain.GetPixelRaw(idx), _gpuBlurField.SampleAsByte(x, y), target, idx * 4);
            }
        }
    }

    private static void WriteTerrainAux(Core.Terrain.TerrainGrid terrain, int idx, Core.Terrain.TerrainPixel pixel, byte sdfValue, byte[] target, int writeIndex)
    {
        byte energy = 0;
        byte scorched = 0;
        switch (pixel)
        {
            case Core.Terrain.TerrainPixel.EnergyLow:
                energy = Tweaks.Screen.PostEmissiveEnergyLow;
                break;
            case Core.Terrain.TerrainPixel.EnergyMedium:
                energy = Tweaks.Screen.PostEmissiveEnergyMedium;
                break;
            case Core.Terrain.TerrainPixel.EnergyHigh:
                energy = Tweaks.Screen.PostEmissiveEnergyHigh;
                break;
            case Core.Terrain.TerrainPixel.DecalHigh:
                scorched = Tweaks.Screen.PostEmissiveScorchedHigh;
                break;
            case Core.Terrain.TerrainPixel.DecalLow:
                scorched = Tweaks.Screen.PostEmissiveScorchedLow;
                break;
        }

        target[writeIndex] = terrain.GetHeat(idx);
        target[writeIndex + 1] = sdfValue;
        target[writeIndex + 2] = energy;
        target[writeIndex + 3] = scorched;
    }

    private void ScrollTerrainBuffer(Offset cameraDelta, Size viewSize)
    {
        int dx = cameraDelta.X;
        int dy = cameraDelta.Y;
        int w = viewSize.X;
        int h = viewSize.Y;
        int srcX = Math.Max(0, dx);
        int srcY = Math.Max(0, dy);
        int dstX = Math.Max(0, -dx);
        int dstY = Math.Max(0, -dy);
        int copyW = w - Math.Abs(dx);
        int copyH = h - Math.Abs(dy);

        if (dy >= 0)
        {
            for (int i = 0; i < copyH; i++)
            {
                Array.Copy(_hiResTerrainPixels, (srcY + i) * w + srcX,
                           _hiResTerrainPixels, (dstY + i) * w + dstX, copyW);
            }
        }
        else
        {
            for (int i = copyH - 1; i >= 0; i--)
            {
                Array.Copy(_hiResTerrainPixels, (srcY + i) * w + srcX,
                           _hiResTerrainPixels, (dstY + i) * w + dstX, copyW);
            }
        }
    }

    private void RenderExposedStrips(uint[] buf, in RenderView view, Offset cameraDelta)
    {
        int w = view.ViewSize.X;
        int h = view.ViewSize.Y;
        int dx = cameraDelta.X;
        int dy = cameraDelta.Y;
        if (dx > 0)
        {
            _hiResTerrainRenderer.RenderStrip(
                _world.Terrain,
                buf,
                view,
                _hiResQuality,
                new Rect(w - dx, 0, dx, h));
        }
        else if (dx < 0)
        {
            _hiResTerrainRenderer.RenderStrip(
                _world.Terrain,
                buf,
                view,
                _hiResQuality,
                new Rect(0, 0, -dx, h));
        }

        if (dy > 0)
        {
            _hiResTerrainRenderer.RenderStrip(
                _world.Terrain,
                buf,
                view,
                _hiResQuality,
                new Rect(0, h - dy, w, dy));
        }
        else if (dy < 0)
        {
            _hiResTerrainRenderer.RenderStrip(
                _world.Terrain,
                buf,
                view,
                _hiResQuality,
                new Rect(0, 0, w, -dy));
        }
    }

    private void LoadHudSprites()
    {
        string baseDir = Path.Combine(AppContext.BaseDirectory, "resources", "hud");
        nint Load(string name) => _textures.LoadTexture(Path.Combine(baseDir, name));

        _hud.Init(
            energyIcon: Load("energy_icon.png"),
            shieldIcon: Load("shield_icon.png"),
            panelFrame: Load("panel_frame.png"),
            buildPanel: Load("build_panel.png"),
            digitStrip: _textures.LoadTexture(Path.Combine(baseDir, "digits.png")));
    }

    private DirectionF? ComputeAimDirection(IReadOnlyList<Core.Entities.Tank> tanks)
    {
        if (tanks.Count == 0) return null;
        var tank = tanks[0];

        if (!IsMouseInViewport(out float relX, out float relY))
            return null;

        int scale = Tweaks.Screen.PixelScale;
        float aimWorldX = (_camPixelX + relX) / scale;
        float aimWorldY = (_camPixelY + relY) / scale;

        float dx = aimWorldX - tank.Position.X;
        float dy = aimWorldY - tank.Position.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return null;
        return new DirectionF(dx / len, dy / len);
    }

    private bool IsMouseInViewport(out float relX, out float relY)
    {
        var (mx, my, _) = _renderer.GetMouseState();
        (float x, float y, float w, float h) vp = _renderBackend.SupportsUi
            ? _hud.ViewportRect
            : (0f, 0f, _hiResSize.X, _hiResSize.Y);

        relX = 0; relY = 0;
        if (vp.w <= 0 || vp.h <= 0) return false;

        relX = mx - vp.x;
        relY = my - vp.y;
        if (relX < 0 || relY < 0 || relX >= vp.w || relY >= vp.h)
            return false;

        return true;
    }

    private bool IsLeftMouseDown()
    {
        var (_, _, buttons) = _renderer.GetMouseState();
        return (buttons & 1) != 0;
    }

    private void EnsureHiResBuffer(Size size)
    {
        if (_hiResSize == size && _hiResPixels.Length == size.Area)
            return;

        _hiResSize = size;
        _hiResPixels = new uint[size.Area];
        _hiResTerrainPixels = new uint[size.Area];
        _hiResTerrainNeedsFullRender = true;
    }

    private static TerrainVisualMode ResolveTerrainVisualMode()
    {
        string? env = Environment.GetEnvironmentVariable("TUNNERER_TERRAIN_VISUAL");
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (string.Equals(env, "legacy", StringComparison.OrdinalIgnoreCase))
                return TerrainVisualMode.LegacyHiRes;
            if (string.Equals(env, "native", StringComparison.OrdinalIgnoreCase))
                return TerrainVisualMode.NativeContinuous;
        }

        return Tweaks.Screen.TerrainVisual;
    }

    private void UpdateHiResQuality(double frameMs)
    {
        float budget = Tweaks.Screen.HiResRenderBudgetMs;
        int hysteresis = Tweaks.Screen.HiResBudgetHysteresisFrames;
        float underThreshold = Tweaks.Screen.HiResBudgetUnderThreshold;
        int qualityIncreaseMultiplier = Tweaks.Screen.HiResQualityIncreaseFramesMultiplier;

        if (frameMs > budget)
        {
            _overBudgetFrames++;
            _underBudgetFrames = 0;
        }
        else if (frameMs < budget * underThreshold)
        {
            _underBudgetFrames++;
            _overBudgetFrames = 0;
        }
        else
        {
            _overBudgetFrames = 0;
            _underBudgetFrames = 0;
        }

        if (_overBudgetFrames >= hysteresis && _hiResQuality > HiResRenderQuality.Low)
        {
            _hiResQuality--;
            _overBudgetFrames = 0;
            _underBudgetFrames = 0;
            Console.WriteLine($"[Render] Hi-res quality reduced to {_hiResQuality}");
        }
        else if (_underBudgetFrames >= hysteresis * qualityIncreaseMultiplier && _hiResQuality < HiResRenderQuality.High)
        {
            _hiResQuality++;
            _overBudgetFrames = 0;
            _underBudgetFrames = 0;
            Console.WriteLine($"[Render] Hi-res quality increased to {_hiResQuality}");
        }
    }

    private void UpdateNativeContinuousQuality(double frameMs)
    {
        float budget = Tweaks.Screen.NativeContinuousRenderBudgetMs;
        int hysteresis = Tweaks.Screen.NativeContinuousBudgetHysteresisFrames;
        if (frameMs > budget)
        {
            _nativeOverBudgetFrames++;
            _nativeUnderBudgetFrames = 0;
        }
        else if (frameMs < budget * Tweaks.Screen.HiResBudgetUnderThreshold)
        {
            _nativeUnderBudgetFrames++;
            _nativeOverBudgetFrames = 0;
        }
        else
        {
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
        }

        if (_nativeOverBudgetFrames >= hysteresis && _nativeContinuousSampleCount > Tweaks.Screen.NativeContinuousSampleLow)
        {
            _nativeContinuousSampleCount = _nativeContinuousSampleCount >= Tweaks.Screen.NativeContinuousSampleHigh
                ? Tweaks.Screen.NativeContinuousSampleMedium
                : Tweaks.Screen.NativeContinuousSampleLow;
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
            Console.WriteLine($"[Render] Native continuous sample count reduced to {_nativeContinuousSampleCount}");
        }
        else if (_nativeUnderBudgetFrames >= hysteresis * Tweaks.Screen.NativeContinuousRecoveryFramesMultiplier &&
                 _nativeContinuousSampleCount < Tweaks.Screen.NativeContinuousSampleHigh)
        {
            _nativeContinuousSampleCount = _nativeContinuousSampleCount <= Tweaks.Screen.NativeContinuousSampleLow
                ? Tweaks.Screen.NativeContinuousSampleMedium
                : Tweaks.Screen.NativeContinuousSampleHigh;
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
            Console.WriteLine($"[Render] Native continuous sample count increased to {_nativeContinuousSampleCount}");
        }
    }

    public void Dispose()
    {
        _textures.Dispose();
        _renderBackend.Dispose();
        _renderer.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void MarkEntityPixels(uint[] terrain, uint[] composite)
    {
        for (int i = 0; i < composite.Length; i++)
        {
            if (composite[i] != terrain[i])
                composite[i] = RenderingPixels.MarkEntity(composite[i]);
        }
    }

    private static void ProfileSection(ref TimeSpan accumulator, Action action)
    {
        var w = Stopwatch.StartNew();
        action();
        accumulator += w.Elapsed;
    }
}

public class DrawProfile
{
    public TimeSpan TerrainDraw;
    public TimeSpan ObjectsDraw;
    public TimeSpan ScreenDraw;
    public TimeSpan ScreenHiResTerrain;
    public TimeSpan ScreenHiResEntities;
    public TimeSpan ScreenUpload;
    public TimeSpan ScreenBackendUpload;
    public TimeSpan ScreenTerrainFullRender;
    public TimeSpan ScreenTerrainScrollCopy;
    public TimeSpan ScreenTerrainExposedStrips;
    public TimeSpan ScreenTerrainDirtyRender;
    public TimeSpan ScreenTerrainToSceneCopy;
    public TimeSpan ScreenEntityRender;
    public TimeSpan ScreenTankGlowBuild;
    public TimeSpan ScreenAuxBuild;
    public TimeSpan ScreenQualityAdjust;
    public TimeSpan ScreenClearFrame;
    public TimeSpan ScreenNewFrame;
    public TimeSpan ScreenHudDraw;
    public TimeSpan ScreenUi;
    public TimeSpan ScreenImGuiRender;
    public TimeSpan ScreenSwap;
    public TimeSpan TotalFrame;
    public int FrameCount;

    public void Report()
    {
        if (FrameCount == 0) return;
        Console.WriteLine($"[Draw]    terrain={Avg(TerrainDraw):F3} objects={Avg(ObjectsDraw):F3} " +
            $"screen={Avg(ScreenDraw):F3} | total={Avg(TotalFrame):F3} ms (avg over {FrameCount} frames)");
        Console.WriteLine($"[Screen]  hiresTerrain={Avg(ScreenHiResTerrain):F3} hiresEntities={Avg(ScreenHiResEntities):F3} " +
            $"upload={Avg(ScreenUpload):F3} ui={Avg(ScreenUi):F3} imguiRender={Avg(ScreenImGuiRender):F3} " +
            $"swap={Avg(ScreenSwap):F3} ms");
        Console.WriteLine($"[Screen+] backendUpload={Avg(ScreenBackendUpload):F3} auxBuild={Avg(ScreenAuxBuild):F3} " +
            $"scrollCopy={Avg(ScreenTerrainScrollCopy):F3} exposedStrips={Avg(ScreenTerrainExposedStrips):F3} " +
            $"dirtyTerrain={Avg(ScreenTerrainDirtyRender):F3} ms");
        Console.WriteLine($"[Screen++] fullTerrain={Avg(ScreenTerrainFullRender):F3} terrainCopy={Avg(ScreenTerrainToSceneCopy):F3} " +
            $"entities={Avg(ScreenEntityRender):F3} tankGlowBuild={Avg(ScreenTankGlowBuild):F3} " +
            $"qualityAdjust={Avg(ScreenQualityAdjust):F3} clear={Avg(ScreenClearFrame):F3} newFrame={Avg(ScreenNewFrame):F3} hud={Avg(ScreenHudDraw):F3} ms");
        Reset();
    }

    private double Avg(TimeSpan ts) => ts.TotalMilliseconds / FrameCount;

    public void Reset()
    {
        TerrainDraw = ObjectsDraw = ScreenDraw = TotalFrame = TimeSpan.Zero;
        ScreenHiResTerrain = ScreenHiResEntities = ScreenUpload = TimeSpan.Zero;
        ScreenBackendUpload = ScreenTerrainFullRender = ScreenAuxBuild = ScreenTerrainScrollCopy = TimeSpan.Zero;
        ScreenTerrainExposedStrips = ScreenTerrainDirtyRender = TimeSpan.Zero;
        ScreenTerrainToSceneCopy = ScreenEntityRender = ScreenTankGlowBuild = TimeSpan.Zero;
        ScreenQualityAdjust = ScreenClearFrame = ScreenNewFrame = ScreenHudDraw = TimeSpan.Zero;
        ScreenUi = ScreenImGuiRender = ScreenSwap = TimeSpan.Zero;
        FrameCount = 0;
    }
}

public readonly record struct PerfCaptureOptions(
    int WarmupFrames,
    int MeasureFrames,
    string? CsvPath);
