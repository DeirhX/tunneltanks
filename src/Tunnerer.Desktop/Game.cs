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
    private bool _gpuAuxFullUploadPending = true;
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

        var renderServices = RenderBackendFactory.CreateServices(
            Tweaks.System.RenderBackend, _renderer.Sdl, _renderer.NativeWindow, windowSize.X, windowSize.Y);
        _renderBackend = renderServices.Backend;
        _textures = renderServices.Textures;
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
            _textures.Dispose();
            _renderBackend.Dispose();
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
            }

            bool hasAuxDirtyRect;
            int auxMinX = 0, auxMinY = 0, auxMaxX = 0, auxMaxY = 0;
            if (_gpuAuxFullUploadPending)
            {
                BuildGpuTerrainAuxData(_world.Terrain, _gpuTerrainAux);
                hasAuxDirtyRect = true;
                auxMinX = 0;
                auxMinY = 0;
                auxMaxX = _terrainSize.X - 1;
                auxMaxY = _terrainSize.Y - 1;
            }
            else
            {
                bool hasTerrainDirty = TryGetDirtyCellBounds(_terrainDirtyCells, out int terrainMinX, out int terrainMinY, out int terrainMaxX, out int terrainMaxY);
                bool hasHeatDirty = _world.Terrain.TryGetHeatDirtyRect(out int heatMinX, out int heatMinY, out int heatMaxX, out int heatMaxY);
                hasAuxDirtyRect = MergeDirtyRects(
                    hasTerrainDirty, terrainMinX, terrainMinY, terrainMaxX, terrainMaxY,
                    hasHeatDirty, heatMinX, heatMinY, heatMaxX, heatMaxY,
                    out auxMinX, out auxMinY, out auxMaxX, out auxMaxY);
                if (hasAuxDirtyRect)
                    BuildGpuTerrainAuxRect(_world.Terrain, _gpuTerrainAux, auxMinX, auxMinY, auxMaxX, auxMaxY);
            }
            _terrainDirtyCells.Clear();

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

            _drawProfile.ScreenHiResEntities += hiResWatch.Elapsed;
            double entityMs = hiResWatch.Elapsed.TotalMilliseconds;

            hiResWatch.Restart();
            var upload = new GamePixelsUpload(
                Pixels: _hiResPixels,
                Width: _hiResSize.X,
                Height: _hiResSize.Y,
                Quality: _hiResQuality,
                TankHeatGlowData: _gpuTankHeatGlow,
                TankHeatGlowCount: tankHeatGlowCount,
                TerrainAux: _gpuTerrainAux,
                WorldWidth: _terrainSize.X,
                WorldHeight: _terrainSize.Y,
                CamPixelX: _camPixelX,
                CamPixelY: _camPixelY,
                PixelScale: scale,
                HasAuxDirtyRect: hasAuxDirtyRect,
                AuxMinX: auxMinX,
                AuxMinY: auxMinY,
                AuxMaxX: auxMaxX,
                AuxMaxY: auxMaxY);
            _renderBackend.UploadGamePixels(upload);
            _world.Terrain.ClearHeatDirtyRect();
            _gpuAuxFullUploadPending = false;
            _drawProfile.ScreenUpload += hiResWatch.Elapsed;
            double uploadMs = hiResWatch.Elapsed.TotalMilliseconds;

            UpdateHiResQuality(terrainMs + entityMs + uploadMs);
        });

        _renderBackend.ClearFrame(winW, winH, 0.1f, 0.1f, 0.1f, 1f);

        var imguiWatch = Stopwatch.StartNew();
        _renderBackend.NewFrame(winW, winH, dt);

        if (player != null)
        {
            var (mx, my, _) = _renderer.GetMouseState();
            var vp = _hud.ViewportRect;
            if (vp.w > 0 && vp.h > 0 &&
                mx >= vp.x && my >= vp.y && mx < vp.x + vp.w && my < vp.y + vp.h)
                _hud.CrosshairScreenPos = (mx, my);
            else
                _hud.CrosshairScreenPos = null;

            _hud.Draw(_renderBackend.GameTextureId, _hiResSize.X, _hiResSize.Y, player, _world, dt);
        }

        _drawProfile.ScreenUi += imguiWatch.Elapsed;

        imguiWatch.Restart();
        _renderBackend.Render();
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
        int count = 0;
        for (int i = 0; i < tanks.Count && count < Tweaks.World.MaxPlayers; i++)
        {
            var tank = tanks[i];
            if (tank.IsDead || tank.Heat < Tweaks.Screen.PostTankHeatGlowMinHeat) continue;

            float t = tank.Heat / Tweaks.Tank.HeatMax;
            float intensity = t * t;
            float glowRadiusPx = pixelScale * (Tweaks.Screen.PostTankHeatGlowBaseRadius + Tweaks.Screen.PostTankHeatGlowScaleRadius * t);
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

    private static bool TryGetDirtyCellBounds(IReadOnlyList<Position> dirtyCells, out int minX, out int minY, out int maxX, out int maxY)
    {
        if (dirtyCells.Count == 0)
        {
            minX = minY = maxX = maxY = 0;
            return false;
        }

        minX = int.MaxValue;
        minY = int.MaxValue;
        maxX = int.MinValue;
        maxY = int.MinValue;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            var p = dirtyCells[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return true;
    }

    private static bool MergeDirtyRects(
        bool hasA, int minAX, int minAY, int maxAX, int maxAY,
        bool hasB, int minBX, int minBY, int maxBX, int maxBY,
        out int minX, out int minY, out int maxX, out int maxY)
    {
        if (!hasA && !hasB)
        {
            minX = minY = maxX = maxY = 0;
            return false;
        }

        if (!hasA)
        {
            minX = minBX; minY = minBY; maxX = maxBX; maxY = maxBY;
            return true;
        }
        if (!hasB)
        {
            minX = minAX; minY = minAY; maxX = maxAX; maxY = maxAY;
            return true;
        }

        minX = Math.Min(minAX, minBX);
        minY = Math.Min(minAY, minBY);
        maxX = Math.Max(maxAX, maxBX);
        maxY = Math.Max(maxAY, maxBY);
        return true;
    }

    private static void BuildGpuTerrainAuxData(Core.Terrain.TerrainGrid terrain, byte[] target)
    {
        int len = terrain.Size.Area;
        for (int i = 0; i < len; i++)
            WriteTerrainAux(terrain, i, terrain.GetPixelRaw(i), target, i * 4);
    }

    private static void BuildGpuTerrainAuxRect(Core.Terrain.TerrainGrid terrain, byte[] target, int minX, int minY, int maxX, int maxY)
    {
        int w = terrain.Width;
        for (int y = minY; y <= maxY; y++)
        {
            int row = y * w;
            for (int x = minX; x <= maxX; x++)
            {
                int idx = row + x;
                WriteTerrainAux(terrain, idx, terrain.GetPixelRaw(idx), target, idx * 4);
            }
        }
    }

    private static void WriteTerrainAux(Core.Terrain.TerrainGrid terrain, int idx, Core.Terrain.TerrainPixel pixel, byte[] target, int writeIndex)
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
        target[writeIndex + 1] = IsSolidTerrainForGpu(pixel) ? (byte)255 : (byte)0;
        target[writeIndex + 2] = energy;
        target[writeIndex + 3] = scorched;
    }

    private static bool IsSolidTerrainForGpu(Core.Terrain.TerrainPixel p)
    {
        if (p == Core.Terrain.TerrainPixel.Blank) return false;
        if (Core.Terrain.Pixel.IsScorched(p)) return false;
        return true;
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
        _renderBackend.Dispose();
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
