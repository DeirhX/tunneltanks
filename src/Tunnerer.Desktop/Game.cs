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
    private readonly ScriptedController? _scriptedController;
    private readonly int _scriptScreenshotFrame;
    private readonly HashSet<int> _scriptScreenshotFrames = [];
    private readonly Dictionary<int, List<GameCommand>> _scriptCommandsByFrame = [];
    private readonly InputRecorder _inputRecorder = new();
    private readonly string? _recordInputPath;
    private readonly bool _recordInputAutoStart;
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
    private bool _showHeatDebugOverlay;
    private bool _showPostPassOverlay = true;
    private PostProcessPassFlags _enabledPostPasses = PostProcessPassFlags.All;
    private Rect _lastAuxViewport;
    private readonly Stopwatch _gameTimer = Stopwatch.StartNew();

    private int _camPixelX;
    private int _camPixelY;

    public const int DefaultSeed = 42;

    private readonly DrawProfile _drawProfile = new();
    private readonly PerfCaptureSession _perfSession;
    private bool _isRunning = true;
    private int _simFrameCounter;

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
        _scriptedController = ScriptedController.TryParse(Environment.GetEnvironmentVariable("TUNNERER_SCRIPTED_INPUT"));
        _scriptScreenshotFrame = ParseNonNegativeInt(Environment.GetEnvironmentVariable("TUNNERER_SCRIPT_SCREENSHOT_FRAME"), -1);
        if (_scriptScreenshotFrame >= 0)
            _scriptScreenshotFrames.Add(_scriptScreenshotFrame);
        ParseScriptScreenshotFrames(Environment.GetEnvironmentVariable("TUNNERER_SCRIPT_SCREENSHOT_FRAMES"), _scriptScreenshotFrames);
        ParseScriptCommands(Environment.GetEnvironmentVariable("TUNNERER_COMMAND_SCRIPT"), _scriptCommandsByFrame);
        _recordInputPath = NormalizeRecordPath(Environment.GetEnvironmentVariable("TUNNERER_RECORD_INPUT_PATH"));
        _recordInputAutoStart = IsTruthy(Environment.GetEnvironmentVariable("TUNNERER_RECORD_INPUT_AUTOSTART"));
        if (_scriptedController is not null)
            Console.WriteLine("[Input] Scripted controller enabled via TUNNERER_SCRIPTED_INPUT.");
        if (_scriptScreenshotFrames.Count > 0)
            Console.WriteLine($"[Input] Scripted screenshot frames enabled ({_scriptScreenshotFrames.Count} frame(s)).");
        if (_scriptCommandsByFrame.Count > 0)
            Console.WriteLine($"[Input] Scripted commands enabled via TUNNERER_COMMAND_SCRIPT ({_scriptCommandsByFrame.Count} frame slot(s)).");
        if (_recordInputPath is not null)
            Console.WriteLine($"[Input] Scripted input recording path: {_recordInputPath}");
        if (_recordInputAutoStart)
        {
            Console.WriteLine("[Input] Scripted input recording autostart enabled.");
            _inputRecorder.Start();
        }
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
                    ApplyScriptCommandsForFrame(_simFrameCounter);

                    var aimDir = ComputeAimDirection(tanks);

                    bool mouseShoot = IsMouseInViewport(out _, out _) && IsLeftMouseDown();
                    ControllerOutput p1Output = default;

                    _world.Advance(i =>
                    {
                        if (i == 0)
                        {
                            var kb = _p1Controller.Poll();
                            ControllerOutput scripted = _scriptedController?.GetOutputAtFrame(_simFrameCounter) ?? default;
                            var move = _scriptedController is null ? kb.MoveSpeed : scripted.MoveSpeed;
                            p1Output = new ControllerOutput
                            {
                                MoveSpeed = move,
                                ShootPrimary = kb.ShootPrimary || mouseShoot || scripted.ShootPrimary,
                                AimDirection = aimDir ?? default,
                            };
                            return p1Output;
                        }
                        var enemy = tanks.Count > 0 ? tanks[0] : null;
                        return _p2AI.GetInput(_world.TankList.Tanks[i], enemy, _world.Terrain);
                    });

                    _inputRecorder.RecordFrame(p1Output.MoveSpeed, p1Output.ShootPrimary);

                    if (_scriptScreenshotFrames.Contains(_simFrameCounter))
                        _renderBackend.RequestScreenshot($"script_frame_{_simFrameCounter:D4}");
                    _simFrameCounter++;

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
            FlushInputRecordingOnExit();
            DisposeResources();
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
        DisposeResources();
        GC.SuppressFinalize(this);
    }

    private void DisposeResources()
    {
        _textures.Dispose();
        _renderBackend.Dispose();
        _renderer.Dispose();
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

    private static int ParseNonNegativeInt(string? value, int fallback)
    {
        if (int.TryParse(value, out int parsed) && parsed >= 0)
            return parsed;
        return fallback;
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeRecordPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        string trimmed = raw.Trim();
        if (Path.IsPathRooted(trimmed))
            return trimmed;
        return Path.GetFullPath(trimmed, Directory.GetCurrentDirectory());
    }

    private static void ParseScriptScreenshotFrames(string? value, HashSet<int> targetFrames)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        string[] tokens = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (int.TryParse(tokens[i], out int frame) && frame >= 0)
                targetFrames.Add(frame);
        }
    }

    private static void ParseScriptCommands(string? value, Dictionary<int, List<GameCommand>> target)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        string[] entries = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            int split = entry.IndexOf(':');
            if (split < 0)
                split = entry.IndexOf('=');
            if (split <= 0 || split >= entry.Length - 1)
                continue;

            string frameToken = entry[..split].Trim();
            string commandToken = entry[(split + 1)..].Trim();
            if (!int.TryParse(frameToken, out int frame) || frame < 0)
                continue;
            if (!TryParseScriptCommand(commandToken, out GameCommand command))
                continue;

            if (!target.TryGetValue(frame, out List<GameCommand>? commands))
            {
                commands = [];
                target[frame] = commands;
            }
            commands.Add(command);
        }
    }

    private static bool TryParseScriptCommand(string token, out GameCommand command)
    {
        return Enum.TryParse(token.Trim(), ignoreCase: true, out command);
    }

    private void ApplyScriptCommandsForFrame(int frame)
    {
        if (!_scriptCommandsByFrame.TryGetValue(frame, out List<GameCommand>? commands))
            return;

        for (int i = 0; i < commands.Count; i++)
            ExecuteGameCommand(commands[i], "script");
    }
}
