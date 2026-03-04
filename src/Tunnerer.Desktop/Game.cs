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
using System.Runtime.InteropServices;

public partial class Game : IDisposable
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
    private int _heatAuxFrameCounter;
    private bool _showThermalRegionDebug;
    private Rect _lastAuxViewport;
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
                if (!_renderer.PollEvents(HandleEvent))
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
                        ApplyThermalRegionDebugOverlay(_compositePixels);
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
