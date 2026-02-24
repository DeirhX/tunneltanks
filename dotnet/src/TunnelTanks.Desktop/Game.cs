namespace TunnelTanks.Desktop;

using Silk.NET.OpenGL;
using Silk.NET.SDL;
using TunnelTanks.Core;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Input;
using TunnelTanks.Core.LevelGen;
using TunnelTanks.Core.Types;
using Surface = TunnelTanks.Core.Types.Surface;
using TunnelTanks.Desktop.Gui;
using TunnelTanks.Desktop.Input;
using TunnelTanks.Desktop.Rendering;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class Game : IDisposable
{
    private readonly SdlRenderer _renderer;
    private readonly GL _gl;
    private readonly ImGuiController _imgui;
    private readonly GameHud _hud;

    private readonly Size _terrainSize;
    private readonly World _world;
    private readonly uint[] _worldPixels;
    private readonly uint[] _compositePixels;
    private readonly KeyboardController _p1Controller;
    private readonly TwitchAI _p2AI;
    public const int DefaultSeed = 42;

    private readonly DrawProfile _drawProfile = new();
    private bool _isRunning = true;

    public unsafe Game(Size? terrainSizeOverride = null, LevelGenMode genMode = LevelGenMode.Deterministic)
    {
        var windowSize = Tweaks.Screen.WindowSize;
        _terrainSize = terrainSizeOverride ?? Tweaks.Screen.RenderSurfaceSize;
        bool parallel = genMode == LevelGenMode.Optimized;

        _renderer = new SdlRenderer(Tweaks.System.WindowTitle, windowSize);

        _gl = GL.GetApi(name =>
            (nint)_renderer.Sdl.GLGetProcAddress(name));

        _imgui = new ImGuiController(_gl, _renderer.Sdl, _renderer.NativeWindow,
            windowSize.X, windowSize.Y);
        _hud = new GameHud();

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
        if (parallel)
            _world.Terrain.DrawAllToSurfaceParallel(_worldPixels);
        else
            _world.Terrain.DrawAllToSurface(_worldPixels);

        _p1Controller = new KeyboardController(_renderer.Sdl, new KeyBindings(
            Left: Scancode.ScancodeA, Right: Scancode.ScancodeD,
            Up: Scancode.ScancodeW, Down: Scancode.ScancodeS,
            Shoot: Scancode.ScancodeSpace));
    }

    public void Run()
    {
        try
        {
            var frameTimer = Stopwatch.StartNew();
            var targetFrameTime = Tweaks.World.AdvanceStep;
            var lastFrameTime = Stopwatch.StartNew();
            var tanks = _world.TankList.Tanks;

            while (_isRunning)
            {
                if (!_renderer.PollEvents(ev => _imgui.ProcessEvent(ev)))
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
                        return _p2AI.GetInput(_world.TankList.Tanks[i]);
                    });

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
                    });

                    _drawProfile.TotalFrame += totalFrameWatch.Elapsed;
                    _drawProfile.FrameCount++;
                    if (_drawProfile.FrameCount >= 100)
                        _drawProfile.Report();
                }

                RenderImGuiFrame(tanks);
            }
        }
        finally
        {
            _imgui.Dispose();
            _renderer.Dispose();
        }
    }

    private void RenderImGuiFrame(IReadOnlyList<Core.Entities.Tank> tanks)
    {
        var (winW, winH) = _renderer.GetWindowSize();
        float dt = 1f / Tweaks.Perf.TargetFps;

        _imgui.UploadGamePixels(_compositePixels, _terrainSize.X, _terrainSize.Y);

        _gl.Viewport(0, 0, (uint)winW, (uint)winH);
        _gl.ClearColor(0.1f, 0.1f, 0.1f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _imgui.NewFrame(winW, winH, dt);

        var player = tanks.Count > 0 ? tanks[0] : null;
        if (player != null)
        {
            var (mx, my, _) = _renderer.GetMouseState();
            var vp = _hud.ViewportRect;
            if (vp.w > 0 && vp.h > 0 &&
                mx >= vp.x && my >= vp.y && mx < vp.x + vp.w && my < vp.y + vp.h)
                _hud.CrosshairScreenPos = (mx, my);
            else
                _hud.CrosshairScreenPos = null;

            _hud.Draw((nint)_imgui.GameTextureId, _terrainSize.X, _terrainSize.Y, player, _world);
        }

        _imgui.Render();
        _renderer.SwapWindow();
    }

    private DirectionF? ComputeAimDirection(IReadOnlyList<Core.Entities.Tank> tanks)
    {
        if (tanks.Count == 0) return null;
        var tank = tanks[0];

        if (!IsMouseInViewport(out float normX, out float normY))
            return null;

        float aimWorldX = normX * _terrainSize.X;
        float aimWorldY = normY * _terrainSize.Y;

        float dx = aimWorldX - tank.Position.X;
        float dy = aimWorldY - tank.Position.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return null;
        return new DirectionF(dx / len, dy / len);
    }

    private bool IsMouseInViewport(out float normX, out float normY)
    {
        var (mx, my, _) = _renderer.GetMouseState();
        var vp = _hud.ViewportRect;

        normX = 0; normY = 0;
        if (vp.w <= 0 || vp.h <= 0) return false;

        float relX = mx - vp.x;
        float relY = my - vp.y;
        if (relX < 0 || relY < 0 || relX >= vp.w || relY >= vp.h)
            return false;

        normX = relX / vp.w;
        normY = relY / vp.h;
        return true;
    }

    private bool IsLeftMouseDown()
    {
        var (_, _, buttons) = _renderer.GetMouseState();
        return (buttons & 1) != 0;
    }

    public void Dispose()
    {
        _imgui.Dispose();
        _renderer.Dispose();
        GC.SuppressFinalize(this);
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
