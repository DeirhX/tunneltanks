namespace Tunnerer.Desktop;

using Silk.NET.SDL;
using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Input;
using Tunnerer.Core.LevelGen;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Gui;
using Tunnerer.Desktop.Config;
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
    private readonly WorldCompositeRenderer _compositeRenderer = new();

    private readonly Size _terrainSize;
    private readonly World _world;
    private readonly uint[] _worldPixels;
    private readonly uint[] _compositePixels;
    private readonly KeyboardController _p1Controller;
    private readonly BotTankAI _p2AI;
    private readonly List<Position> _terrainDirtyCells = new();
    private readonly float[] _gpuTankHeatGlow = new float[Tweaks.World.MaxPlayers * 4];
    private readonly byte[] _gpuTerrainAux;
    private readonly TerrainBlurField _gpuBlurField = new();
    private bool _gpuAuxFullUploadPending = true;
    private Size _hiResSize;
    private int _nativeContinuousSampleCount = DesktopScreenTweaks.NativeContinuousSampleHigh;
    private int _nativeOverBudgetFrames;
    private int _nativeUnderBudgetFrames;
    private readonly Stopwatch _gameTimer = Stopwatch.StartNew();

    private int _camPixelX;
    private int _camPixelY;

    public const int DefaultSeed = 42;

    private readonly DrawProfile _drawProfile = new();
    private readonly PerfCaptureSession _perfSession;
    private bool _isRunning = true;

    public unsafe Game(
        Size? terrainSizeOverride = null,
        LevelGenMode genMode = LevelGenMode.Deterministic,
        PerfCaptureOptions? perfCapture = null,
        RenderBackendKind? renderBackendOverride = null)
    {
        var windowSize = DesktopScreenTweaks.WindowSize;
        _terrainSize = terrainSizeOverride ?? DesktopScreenTweaks.RenderSurfaceSize;
        RenderBackendKind selectedBackend = renderBackendOverride ?? DesktopTweaks.DefaultRenderBackend;
        bool deterministicSimulation = genMode == LevelGenMode.Deterministic;
        bool parallel = genMode == LevelGenMode.Optimized;
        _perfSession = PerfCaptureSession.Create(perfCapture);

        _renderer = new SdlRenderer(Tweaks.System.WindowTitle, windowSize);

        var renderServices = RenderBackendFactory.CreateServices(
            selectedBackend, _renderer.Sdl, _renderer.NativeWindow);
        _renderBackend = renderServices.Backend;
        _textures = renderServices.Textures;
        Console.WriteLine($"[Render] Backend={selectedBackend}");
        _hud = new GameHud();
        LoadHudSprites();

        var modeLabel = parallel ? "optimized" : "deterministic";
        Console.WriteLine($"Generating terrain {_terrainSize.X}x{_terrainSize.Y} ({modeLabel}, seed={DefaultSeed})...");
        var genWatch = Stopwatch.StartNew();
        var generator = new ToastGenerator();
        int? seed = deterministicSimulation ? DefaultSeed : null;
        var (terrain, spawns) = generator.Generate(_terrainSize, seed, genMode);
        Console.WriteLine($"Level generated in {genWatch.Elapsed.TotalMilliseconds:F0} ms, {spawns.Length} spawns");

        _world = new World(
            _terrainSize,
            deterministicSimulation: deterministicSimulation,
            simulationSeed: deterministicSimulation ? DefaultSeed : 0);
        int? matSeed = deterministicSimulation ? DefaultSeed + 1 : null;
        _world.Initialize(terrain, spawns, materializeSeed: matSeed, parallelMaterialize: parallel);
        _p2AI = new BotTankAI(seed: deterministicSimulation ? DefaultSeed + 2 : null);

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
        Console.WriteLine("[Render] TerrainVisual=NativeContinuous");
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
                        _compositeRenderer.Compose(_world, _worldPixels, _compositePixels);
                        MarkEntityPixels(_worldPixels, _compositePixels);
                    });

                    RenderImGuiFrame(tanks);

                    _drawProfile.TotalFrame += totalFrameWatch.Elapsed;
                    _drawProfile.FrameCount++;
                    if (_drawProfile.FrameCount >= 100)
                        _drawProfile.Report();

                    if (_perfSession.Capture(totalFrameWatch.Elapsed))
                        _isRunning = false;
                }
            }

            _perfSession.ReportIfEnabled();
        }
        finally
        {
            _textures.Dispose();
            _renderBackend.Dispose();
            _renderer.Dispose();
        }
    }

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
            float minVisible = DesktopScreenTweaks.PostTankHeatGlowMinHeat / Tweaks.Tank.HeatMax;
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
            float glowRadiusPx = pixelScale * (DesktopScreenTweaks.PostTankHeatGlowBaseRadius + DesktopScreenTweaks.PostTankHeatGlowScaleRadius * radiusFactor);
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
                energy = DesktopScreenTweaks.PostEmissiveEnergyLow;
                break;
            case Core.Terrain.TerrainPixel.EnergyMedium:
                energy = DesktopScreenTweaks.PostEmissiveEnergyMedium;
                break;
            case Core.Terrain.TerrainPixel.EnergyHigh:
                energy = DesktopScreenTweaks.PostEmissiveEnergyHigh;
                break;
            case Core.Terrain.TerrainPixel.DecalHigh:
                scorched = DesktopScreenTweaks.PostEmissiveScorchedHigh;
                break;
            case Core.Terrain.TerrainPixel.DecalLow:
                scorched = DesktopScreenTweaks.PostEmissiveScorchedLow;
                break;
        }

        float heat = terrain.GetHeatTemperature(idx);
        target[writeIndex] = (byte)Math.Clamp((int)MathF.Round(heat), 0, 255);
        target[writeIndex + 1] = sdfValue;
        target[writeIndex + 2] = energy;
        target[writeIndex + 3] = scorched;
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

        int scale = DesktopScreenTweaks.PixelScale;
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

        if (_renderBackend.SupportsUi)
        {
            // Input space must match the simulation render view, even if the HUD viewport
            // is showing a texture whose displayed size differs from _hiResSize.
            float sx = _hiResSize.X / Math.Max(1f, vp.w);
            float sy = _hiResSize.Y / Math.Max(1f, vp.h);
            relX *= sx;
            relY *= sy;
        }

        return true;
    }

    private bool IsLeftMouseDown()
    {
        var (_, _, buttons) = _renderer.GetMouseState();
        return (buttons & 1) != 0;
    }

    private void EnsureHiResBuffer(Size size)
    {
        if (_hiResSize == size)
            return;

        _hiResSize = size;
    }

    private void UpdateNativeContinuousQuality(double frameMs)
    {
        float budget = DesktopScreenTweaks.NativeContinuousRenderBudgetMs;
        int hysteresis = DesktopScreenTweaks.NativeContinuousBudgetHysteresisFrames;
        if (frameMs > budget)
        {
            _nativeOverBudgetFrames++;
            _nativeUnderBudgetFrames = 0;
        }
        else if (frameMs < budget * DesktopScreenTweaks.NativeContinuousBudgetUnderThreshold)
        {
            _nativeUnderBudgetFrames++;
            _nativeOverBudgetFrames = 0;
        }
        else
        {
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
        }

        if (_nativeOverBudgetFrames >= hysteresis && _nativeContinuousSampleCount > DesktopScreenTweaks.NativeContinuousSampleLow)
        {
            _nativeContinuousSampleCount = _nativeContinuousSampleCount >= DesktopScreenTweaks.NativeContinuousSampleHigh
                ? DesktopScreenTweaks.NativeContinuousSampleMedium
                : DesktopScreenTweaks.NativeContinuousSampleLow;
            _nativeOverBudgetFrames = 0;
            _nativeUnderBudgetFrames = 0;
            Console.WriteLine($"[Render] Native continuous sample count reduced to {_nativeContinuousSampleCount}");
        }
        else if (_nativeUnderBudgetFrames >= hysteresis * DesktopScreenTweaks.NativeContinuousRecoveryFramesMultiplier &&
                 _nativeContinuousSampleCount < DesktopScreenTweaks.NativeContinuousSampleHigh)
        {
            _nativeContinuousSampleCount = _nativeContinuousSampleCount <= DesktopScreenTweaks.NativeContinuousSampleLow
                ? DesktopScreenTweaks.NativeContinuousSampleMedium
                : DesktopScreenTweaks.NativeContinuousSampleHigh;
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
            $"tankGlowBuild={Avg(ScreenTankGlowBuild):F3} " +
            $"qualityAdjust={Avg(ScreenQualityAdjust):F3} clear={Avg(ScreenClearFrame):F3} newFrame={Avg(ScreenNewFrame):F3} hud={Avg(ScreenHudDraw):F3} ms");
        Reset();
    }

    private double Avg(TimeSpan ts) => ts.TotalMilliseconds / FrameCount;

    public void Reset()
    {
        TerrainDraw = ObjectsDraw = ScreenDraw = TotalFrame = TimeSpan.Zero;
        ScreenHiResTerrain = ScreenHiResEntities = ScreenUpload = TimeSpan.Zero;
        ScreenBackendUpload = ScreenAuxBuild = ScreenTankGlowBuild = TimeSpan.Zero;
        ScreenQualityAdjust = ScreenClearFrame = ScreenNewFrame = ScreenHudDraw = TimeSpan.Zero;
        ScreenUi = ScreenImGuiRender = ScreenSwap = TimeSpan.Zero;
        FrameCount = 0;
    }
}

internal sealed class PerfCaptureSession
{
    private readonly PerfCaptureOptions? _options;
    private readonly List<double> _frameMs;
    private int _framesSeen;

    private PerfCaptureSession(PerfCaptureOptions? options)
    {
        _options = options;
        _frameMs = options is PerfCaptureOptions o ? new List<double>(o.MeasureFrames) : [];
    }

    public static PerfCaptureSession Create(PerfCaptureOptions? options)
    {
        if (options is not PerfCaptureOptions perf)
            return new PerfCaptureSession(null);

        var normalized = new PerfCaptureOptions(
            WarmupFrames: Math.Max(0, perf.WarmupFrames),
            MeasureFrames: Math.Max(1, perf.MeasureFrames),
            CsvPath: string.IsNullOrWhiteSpace(perf.CsvPath) ? null : perf.CsvPath);
        Console.WriteLine($"[Perf] enabled warmup={normalized.WarmupFrames} measure={normalized.MeasureFrames}");
        return new PerfCaptureSession(normalized);
    }

    public bool Capture(TimeSpan totalFrame)
    {
        if (_options is not PerfCaptureOptions perf)
            return false;

        _framesSeen++;
        if (_framesSeen > perf.WarmupFrames)
            _frameMs.Add(totalFrame.TotalMilliseconds);

        return _frameMs.Count >= perf.MeasureFrames;
    }

    public void ReportIfEnabled()
    {
        if (_options is not PerfCaptureOptions perf || _frameMs.Count == 0)
            return;

        double sum = 0;
        for (int i = 0; i < _frameMs.Count; i++)
            sum += _frameMs[i];

        double[] sorted = _frameMs.ToArray();
        Array.Sort(sorted);
        double avg = sum / _frameMs.Count;
        double min = sorted[0];
        double max = sorted[^1];
        double p50 = Percentile(sorted, 0.50);
        double p95 = Percentile(sorted, 0.95);
        double p99 = Percentile(sorted, 0.99);

        Console.WriteLine(
            $"[Perf] frames={_frameMs.Count} warmup={perf.WarmupFrames} avg={avg:F3}ms " +
            $"p50={p50:F3}ms p95={p95:F3}ms p99={p99:F3}ms min={min:F3}ms max={max:F3}ms");

        if (perf.CsvPath is null)
            return;

        string fullPath = Path.GetFullPath(perf.CsvPath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(fullPath, false);
        writer.WriteLine("frame,ms");
        for (int i = 0; i < _frameMs.Count; i++)
            writer.WriteLine($"{i},{_frameMs[i].ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"[Perf] wrote csv: {fullPath}");
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0)
            return 0;
        int idx = (int)Math.Ceiling(sorted.Length * p) - 1;
        idx = Math.Clamp(idx, 0, sorted.Length - 1);
        return sorted[idx];
    }
}

public readonly record struct PerfCaptureOptions(
    int WarmupFrames,
    int MeasureFrames,
    string? CsvPath);
