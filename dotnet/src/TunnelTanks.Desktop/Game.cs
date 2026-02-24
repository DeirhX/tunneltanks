namespace TunnelTanks.Desktop;

using Silk.NET.SDL;
using TunnelTanks.Core;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Gui;
using TunnelTanks.Core.Input;
using TunnelTanks.Core.LevelGen;
using TunnelTanks.Core.Types;
using TunnelTanks.Desktop.Input;
using TunnelTanks.Desktop.Rendering;
using System.Diagnostics;

public class Game
{
    private readonly SdlRenderer _renderer;
    private readonly Sdl _sdl;

    private readonly Size _renderSize;
    private readonly Size _terrainSize;
    private readonly World _world;
    private readonly Screen _screen;
    private readonly uint[] _worldPixels;
    private readonly uint[] _compositePixels;
    private readonly uint[] _screenPixels;
    private readonly KeyboardController _p1Controller;
    private readonly TwitchAI _p2AI;
    public const int DefaultSeed = 42;

    private readonly DrawProfile _drawProfile = new();
    private bool _isRunning = true;

    public Game(Size? terrainSizeOverride = null, LevelGenMode genMode = LevelGenMode.Deterministic)
    {
        _renderSize = Tweaks.Screen.RenderSurfaceSize;
        _terrainSize = terrainSizeOverride ?? _renderSize;
        var windowSize = Tweaks.Screen.WindowSize;
        bool parallel = genMode == LevelGenMode.Optimized;

        _renderer = new SdlRenderer(Tweaks.System.WindowTitle, windowSize, _renderSize);
        _sdl = Sdl.GetApi();

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
        _p2AI = new TwitchAI(seed: parallel ? null : DefaultSeed + 2);

        _worldPixels = new uint[_terrainSize.Area];
        _compositePixels = new uint[_terrainSize.Area];
        _screenPixels = new uint[_renderSize.Area];
        if (parallel)
            _world.Terrain.DrawAllToSurfaceParallel(_worldPixels);
        else
            _world.Terrain.DrawAllToSurface(_worldPixels);

        _p1Controller = new KeyboardController(_sdl,
            Scancode.ScancodeA, Scancode.ScancodeD,
            Scancode.ScancodeW, Scancode.ScancodeS,
            Scancode.ScancodeSpace);

        _screen = new Screen(_renderSize);
        var tanks = _world.TankList.Tanks;
        if (tanks.Count >= 2)
            _screen.SetupTwoPlayers(tanks[0], tanks[1]);
        else if (tanks.Count == 1)
            _screen.SetupSinglePlayer(tanks[0]);
    }

    public void Run()
    {
        var frameTimer = Stopwatch.StartNew();
        var targetFrameTime = Tweaks.World.AdvanceStep;

        while (_isRunning)
        {
            if (!_renderer.PollEvents())
            { _isRunning = false; break; }

            if (_world.IsGameOver)
            { _isRunning = false; break; }

            if (frameTimer.Elapsed >= targetFrameTime)
            {
                frameTimer.Restart();
                var totalFrameWatch = Stopwatch.StartNew();

                var (mx, my, mbuttons) = _renderer.GetMouseState();
                bool mouseShoot = (mbuttons & 1) != 0;
                var aimDir = _screen.SetCrosshairScreenPos(mx, my, 0);

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
                    return _p2AI.GetInput(_world.TankList.Tanks[i]);
                });

                { var w = Stopwatch.StartNew(); _world.Terrain.DrawChangesToSurface(_worldPixels); _drawProfile.TerrainDraw += w.Elapsed; }

                {
                    var w = Stopwatch.StartNew();
                    Array.Copy(_worldPixels, _compositePixels, _worldPixels.Length);
                    _world.LinkMap.Draw(_compositePixels, _terrainSize.X, _terrainSize.Y);
                    _world.Machines.Draw(_compositePixels, _terrainSize.X, _terrainSize.Y);
                    _world.Projectiles.Draw(_compositePixels, _terrainSize.X, _terrainSize.Y);
                    _world.Sprites.Draw(_compositePixels, _terrainSize.X, _terrainSize.Y);
                    _world.TankList.Draw(_compositePixels, _terrainSize.X, _terrainSize.Y);
                    _drawProfile.ObjectsDraw += w.Elapsed;
                }

                {
                    var w = Stopwatch.StartNew();
                    _screen.Draw(_compositePixels, _terrainSize.X, _terrainSize.Y,
                                 _screenPixels, _renderSize.X, _renderSize.Y);
                    _drawProfile.ScreenDraw += w.Elapsed;
                }

                _drawProfile.TotalFrame += totalFrameWatch.Elapsed;
                _drawProfile.FrameCount++;
                if (_drawProfile.FrameCount >= 100)
                    _drawProfile.Report();
            }

            _renderer.RenderFrame(_screenPixels);
        }

        _renderer.Dispose();
    }
}

public class DrawProfile
{
    public TimeSpan TerrainDraw;
    public TimeSpan ObjectsDraw;
    public TimeSpan ScreenDraw;
    public TimeSpan TotalFrame;
    public int FrameCount;

    public void Report()
    {
        if (FrameCount == 0) return;
        Console.WriteLine($"[Draw]    terrain={Avg(TerrainDraw):F3} objects={Avg(ObjectsDraw):F3} " +
            $"screen={Avg(ScreenDraw):F3} | total={Avg(TotalFrame):F3} ms (avg over {FrameCount} frames)");
        Reset();
    }

    private double Avg(TimeSpan ts) => ts.TotalMilliseconds / FrameCount;

    public void Reset()
    {
        TerrainDraw = ObjectsDraw = ScreenDraw = TotalFrame = TimeSpan.Zero;
        FrameCount = 0;
    }
}
