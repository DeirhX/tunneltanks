namespace Tunnerer.Desktop;

using Silk.NET.OpenGL;
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
using System.Runtime.InteropServices;

public class Game : IDisposable
{
    private readonly SdlRenderer _renderer;
    private readonly GL _gl;
    private readonly ImGuiController _imgui;
    private readonly TextureManager _textures;
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
    private readonly byte[] _gpuTerrainHeat;
    private uint[] _hiResPixels = Array.Empty<uint>();
    private uint[] _hiResTerrainPixels = Array.Empty<uint>();
    private Size _hiResSize;
    private bool _hiResTerrainNeedsFullRender = true;
    private HiResRenderQuality _hiResQuality =
        (HiResRenderQuality)Math.Clamp(Tweaks.Screen.HiResInitialQuality, 0, 2);
    private int _overBudgetFrames;
    private int _underBudgetFrames;
    private readonly Stopwatch _gameTimer = Stopwatch.StartNew();

    private int _camPixelX;
    private int _camPixelY;
    private int _prevCamPixelX = -1;
    private int _prevCamPixelY = -1;

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
        _textures = new TextureManager(_gl);
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
        _gpuTerrainHeat = new byte[_terrainSize.Area];
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
                    });

                    RenderImGuiFrame(tanks);

                    _drawProfile.TotalFrame += totalFrameWatch.Elapsed;
                    _drawProfile.FrameCount++;
                    if (_drawProfile.FrameCount >= 100)
                        _drawProfile.Report();
                }
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
        int scale = Tweaks.Screen.PixelScale;

        int viewW = Math.Max(1, winW);
        int viewH = Math.Max(1, winH - (int)GameHud.BottomPanelHeight);
        EnsureHiResBuffer(new Size(viewW, viewH));

        var player = tanks.Count > 0 ? tanks[0] : null;
        UpdateCamera(player, viewW, viewH, scale);

        int dx = _camPixelX - _prevCamPixelX;
        int dy = _camPixelY - _prevCamPixelY;
        bool cameraMoved = dx != 0 || dy != 0;
        _prevCamPixelX = _camPixelX;
        _prevCamPixelY = _camPixelY;

        ProfileSection(ref _drawProfile.ScreenDraw, () =>
        {
            var hiResWatch = Stopwatch.StartNew();
            float renderTime = (float)_gameTimer.Elapsed.TotalSeconds;
            if (_hiResTerrainNeedsFullRender ||
                Math.Abs(dx) >= viewW || Math.Abs(dy) >= viewH)
            {
                _hiResTerrainRenderer.Render(_world.Terrain, _hiResTerrainPixels,
                    _hiResSize.X, _hiResSize.Y, _hiResQuality,
                    _camPixelX, _camPixelY, scale, renderTime);
                _hiResTerrainNeedsFullRender = false;
            }
            else if (cameraMoved)
            {
                ScrollTerrainBuffer(dx, dy, viewW, viewH);
                RenderExposedStrips(_hiResTerrainPixels, viewW, viewH, dx, dy, scale);
            }

            if (_terrainDirtyCells.Count > 0)
            {
                _hiResTerrainRenderer.RenderDirty(_world.Terrain, _hiResTerrainPixels,
                    _hiResSize.X, _hiResSize.Y, _hiResQuality,
                    _camPixelX, _camPixelY, scale,
                    _terrainDirtyCells, renderTime);
                _terrainDirtyCells.Clear();
            }

            Array.Copy(_hiResTerrainPixels, _hiResPixels, _hiResPixels.Length);
            _drawProfile.ScreenHiResTerrain += hiResWatch.Elapsed;
            double terrainMs = hiResWatch.Elapsed.TotalMilliseconds;

            hiResWatch.Restart();
            _hiResEntityRenderer.Render(
                _hiResPixels, _hiResSize.X, _hiResSize.Y,
                _worldPixels, _compositePixels,
                _terrainSize.X, _terrainSize.Y,
                _camPixelX, _camPixelY, scale, renderTime);

            int tankHeatGlowCount = BuildGpuTankHeatGlowData(
                _world.TankList.Tanks, _camPixelX, _camPixelY, scale, _hiResSize.X, _hiResSize.Y);
            BuildGpuTerrainHeatData(_world.Terrain, _gpuTerrainHeat);

            _drawProfile.ScreenHiResEntities += hiResWatch.Elapsed;
            double entityMs = hiResWatch.Elapsed.TotalMilliseconds;

            hiResWatch.Restart();
            _imgui.UploadGamePixels(_hiResPixels, _hiResSize.X, _hiResSize.Y, _hiResQuality,
                _gpuTankHeatGlow, tankHeatGlowCount,
                _gpuTerrainHeat, _terrainSize.X, _terrainSize.Y, _camPixelX, _camPixelY, scale);
            _drawProfile.ScreenUpload += hiResWatch.Elapsed;
            double uploadMs = hiResWatch.Elapsed.TotalMilliseconds;

            UpdateHiResQuality(terrainMs + entityMs + uploadMs);
        });

        _gl.Viewport(0, 0, (uint)winW, (uint)winH);
        _gl.ClearColor(0.1f, 0.1f, 0.1f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        var imguiWatch = Stopwatch.StartNew();
        _imgui.NewFrame(winW, winH, dt);

        if (player != null)
        {
            var (mx, my, _) = _renderer.GetMouseState();
            var vp = _hud.ViewportRect;
            if (vp.w > 0 && vp.h > 0 &&
                mx >= vp.x && my >= vp.y && mx < vp.x + vp.w && my < vp.y + vp.h)
                _hud.CrosshairScreenPos = (mx, my);
            else
                _hud.CrosshairScreenPos = null;

            _hud.Draw((nint)_imgui.GameTextureId, _hiResSize.X, _hiResSize.Y, player, _world, dt);
        }

        _drawProfile.ScreenUi += imguiWatch.Elapsed;

        imguiWatch.Restart();
        _imgui.Render();
        _drawProfile.ScreenImGuiRender += imguiWatch.Elapsed;

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
        int camPixelX, int camPixelY, int pixelScale, int targetW, int targetH)
    {
        const float minHeat = 5f;
        const float baseRadius = 2.5f;
        const float scaleRadius = 2.5f;

        int count = 0;
        for (int i = 0; i < tanks.Count && count < Tweaks.World.MaxPlayers; i++)
        {
            var tank = tanks[i];
            if (tank.IsDead || tank.Heat < minHeat) continue;

            float t = tank.Heat / Tweaks.Tank.HeatMax;
            float intensity = t * t;
            float glowRadiusPx = pixelScale * (baseRadius + scaleRadius * t);
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

    private static void BuildGpuTerrainHeatData(Core.Terrain.TerrainGrid terrain, byte[] target)
    {
        int len = terrain.Size.Area;
        for (int i = 0; i < len; i++)
            target[i] = terrain.GetHeat(i);
    }

    private void ScrollTerrainBuffer(int dx, int dy, int w, int h)
    {
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

    private void RenderExposedStrips(uint[] buf, int w, int h, int dx, int dy, int scale)
    {
        if (dx > 0)
        {
            _hiResTerrainRenderer.RenderStrip(_world.Terrain, buf, w, h, _hiResQuality,
                _camPixelX, _camPixelY, scale, w - dx, 0, w - 1, h - 1);
        }
        else if (dx < 0)
        {
            _hiResTerrainRenderer.RenderStrip(_world.Terrain, buf, w, h, _hiResQuality,
                _camPixelX, _camPixelY, scale, 0, 0, -dx - 1, h - 1);
        }

        if (dy > 0)
        {
            _hiResTerrainRenderer.RenderStrip(_world.Terrain, buf, w, h, _hiResQuality,
                _camPixelX, _camPixelY, scale, 0, h - dy, w - 1, h - 1);
        }
        else if (dy < 0)
        {
            _hiResTerrainRenderer.RenderStrip(_world.Terrain, buf, w, h, _hiResQuality,
                _camPixelX, _camPixelY, scale, 0, 0, w - 1, -dy - 1);
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
        var vp = _hud.ViewportRect;

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

    public void Dispose()
    {
        _textures.Dispose();
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
    public TimeSpan ScreenHiResTerrain;
    public TimeSpan ScreenHiResEntities;
    public TimeSpan ScreenUpload;
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
        Reset();
    }

    private double Avg(TimeSpan ts) => ts.TotalMilliseconds / FrameCount;

    public void Reset()
    {
        TerrainDraw = ObjectsDraw = ScreenDraw = TotalFrame = TimeSpan.Zero;
        ScreenHiResTerrain = ScreenHiResEntities = ScreenUpload = TimeSpan.Zero;
        ScreenUi = ScreenImGuiRender = ScreenSwap = TimeSpan.Zero;
        FrameCount = 0;
    }
}
