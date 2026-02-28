namespace Tunnerer.Core;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Collision;
using Tunnerer.Core.Input;
using Tunnerer.Core.Entities.Projectiles;
using Tunnerer.Core.Entities.Machines;
using Tunnerer.Core.Entities.Links;
using System.Diagnostics;

public class World
{
    private readonly TerrainGrid _terrain;
    private readonly TankBases _tankBases = new();
    private readonly TankList _tankList = new();
    private readonly ProjectileList _projectiles;
    private readonly MachineList _machines = new();
    private readonly LinkMap _linkMap;
    private readonly SpriteList _sprites = new();
    private readonly CollisionSolver _collisionSolver;
    private readonly bool _deterministicSimulation;
    private readonly int _simulationSeed;

    private int _advanceCount;
    private TimeSpan _elapsed;
    private TimeSpan _regrowAccumulator;
    private readonly Stopwatch _regrowTimer = new();
    private bool _gameOver;

    public TerrainGrid Terrain => _terrain;
    public TankBases TankBases => _tankBases;
    public TankList TankList => _tankList;
    public ProjectileList Projectiles => _projectiles;
    public MachineList Machines => _machines;
    public LinkMap LinkMap => _linkMap;
    public SpriteList Sprites => _sprites;
    public CollisionSolver CollisionSolver => _collisionSolver;
    public int AdvanceCount => _advanceCount;
    public bool IsGameOver => _gameOver;

    public SimulationProfile Profile { get; } = new();

    public World(Size terrainSize, bool deterministicSimulation = false, int simulationSeed = 0)
    {
        _deterministicSimulation = deterministicSimulation;
        _simulationSeed = simulationSeed != 0 ? simulationSeed : 0x51A7E3;
        _terrain = new TerrainGrid(terrainSize);
        _projectiles = new ProjectileList(deterministicSimulation ? _simulationSeed ^ 0x5f3759df : null);
        _linkMap = new LinkMap(deterministicSimulation, _simulationSeed ^ 0x13579BDF);
        _collisionSolver = new CollisionSolver(_terrain);
        _regrowTimer.Start();
    }

    public void Initialize(TerrainGrid generatedTerrain, Position[] spawns,
        int? materializeSeed = null, bool parallelMaterialize = false)
    {
        for (int i = 0; i < generatedTerrain.Size.Area; i++)
            _terrain[i] = generatedTerrain[i];

        _terrain.MaterializeTerrain(materializeSeed, parallel: parallelMaterialize);
        _terrain.DecorateTerrain(materializeSeed.HasValue ? materializeSeed.Value + 100 : null);

        for (int i = 0; i < spawns.Length; i++)
            _tankBases.AddBase(spawns[i], i);

        _tankBases.CreateBasesInTerrain(_terrain);

        for (int i = 0; i < spawns.Length; i++)
        {
            var tankBase = _tankBases.GetSpawn(i);
            if (tankBase != null)
            {
                int tankSeed = unchecked(_simulationSeed ^ ((i + 1) * (int)0x9e3779b9u));
                _tankList.AddTank(i, tankBase,
                    _deterministicSimulation ? tankSeed : 0);
            }
        }
    }

    public void Advance(Func<int, ControllerOutput> getInput)
    {
        if (_gameOver) return;

        var frameWatch = Stopwatch.StartNew();

        _advanceCount++;
        _elapsed += Tweaks.World.AdvanceStep;

        _collisionSolver.Update(_tankList, _machines);

        ProfileSection(ref Profile.Regrow, RegrowPass);
        ProfileSection(ref Profile.Projectiles, () => _projectiles.Advance(_collisionSolver));
        ProfileSection(ref Profile.Tanks, () => _tankList.Advance(this, getInput));
        ProfileSection(ref Profile.Harvesters, () => _machines.Advance(_terrain, Tweaks.World.AdvanceStep));
        ProfileSection(ref Profile.Sprites, () => _sprites.Advance(Tweaks.World.AdvanceStep));
        ProfileSection(ref Profile.Bases, () => _tankBases.Advance());
        ProfileSection(ref Profile.Links, () => _linkMap.Advance(_terrain, Tweaks.World.AdvanceStep));

        Profile.Total += frameWatch.Elapsed;
        Profile.FrameCount++;

        if (Profile.FrameCount >= 100)
            Profile.Report();
    }

    public void SetGameOver() => _gameOver = true;

    private static void ProfileSection(ref TimeSpan accumulator, Action action)
    {
        var w = Stopwatch.StartNew();
        action();
        accumulator += w.Elapsed;
    }

    private void RegrowPass()
    {
        if (_deterministicSimulation)
        {
            _regrowAccumulator += Tweaks.World.AdvanceStep;
            if (_regrowAccumulator < Tweaks.World.DirtRecoverInterval)
                return;
            _regrowAccumulator -= Tweaks.World.DirtRecoverInterval;
        }
        else
        {
            if (_regrowTimer.Elapsed < _regrowAccumulator + Tweaks.World.DirtRecoverInterval)
                return;
            _regrowAccumulator += Tweaks.World.DirtRecoverInterval;
        }

        _terrain.CoolDown(Tweaks.World.HeatCooldownPerTick, Tweaks.World.HeatDiffuseRate);

        int w = _terrain.Width, h = _terrain.Height;

        if (_deterministicSimulation)
        {
            var writes = new List<(int offset, TerrainPixel value)>(1024);
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    var pos = new Position(x, y);
                    var pix = _terrain.GetPixelRaw(pos);

                    if (pix == TerrainPixel.Blank)
                    {
                        int neighbors = _terrain.CountDirtNeighbors(pos);
                        if (TryQueueDirtRegrow(x, y, neighbors, Tweaks.World.DirtRegrowBlankModifier, 0))
                            writes.Add((x + y * w, TerrainPixel.DirtGrow));
                    }
                    else if (Pixel.IsScorched(pix))
                    {
                        int neighbors = _terrain.CountDirtNeighbors(pos);
                        if (TryQueueDirtRegrow(x, y, neighbors, Tweaks.World.DirtRegrowScorchedModifier, 1))
                        {
                            writes.Add((x + y * w, TerrainPixel.DirtGrow));
                        }
                        else if (pix == TerrainPixel.DecalHigh && Roll1000(x, y, 2) < Tweaks.World.DecalDecaySpeed)
                        {
                            writes.Add((x + y * w, TerrainPixel.DecalLow));
                        }
                        else if (pix == TerrainPixel.DecalLow && Roll1000(x, y, 3) < Tweaks.World.DecalDecaySpeed)
                        {
                            writes.Add((x + y * w, TerrainPixel.Blank));
                        }
                    }
                    else if (pix == TerrainPixel.DirtGrow)
                    {
                        if (Roll1000(x, y, 4) < Tweaks.World.DirtRecoverSpeed)
                        {
                            var newPix = (Roll1000(x, y, 5) & 1) == 0 ? TerrainPixel.DirtHigh : TerrainPixel.DirtLow;
                            writes.Add((x + y * w, newPix));
                        }
                    }
                }
            }

            foreach (var (offset, value) in writes)
            {
                _terrain.SetPixelRaw(offset, value);
                _terrain.CommitPixel(new Position(offset % w, offset / w));
            }
            return;
        }

        var stagedWrites = new ThreadLocal<List<(int offset, TerrainPixel value)>>(
            () => new List<(int, TerrainPixel)>(), trackAllValues: true);
        var rng = new ThreadLocal<Random>(() => new Random(), trackAllValues: true);

        Parallel.For(1, h - 1, y =>
        {
            var writes = stagedWrites.Value!;
            var random = rng.Value!;

            for (int x = 1; x < w - 1; x++)
            {
                var pos = new Position(x, y);
                var pix = _terrain.GetPixelRaw(pos);

                if (pix == TerrainPixel.Blank)
                {
                    int neighbors = _terrain.CountDirtNeighbors(pos);
                    if (TryQueueDirtRegrow(random, neighbors, Tweaks.World.DirtRegrowBlankModifier))
                        writes.Add((x + y * w, TerrainPixel.DirtGrow));
                }
                else if (Pixel.IsScorched(pix))
                {
                    int neighbors = _terrain.CountDirtNeighbors(pos);
                    if (TryQueueDirtRegrow(random, neighbors, Tweaks.World.DirtRegrowScorchedModifier))
                    {
                        writes.Add((x + y * w, TerrainPixel.DirtGrow));
                    }
                    else if (pix == TerrainPixel.DecalHigh && random.Next(1000) < Tweaks.World.DecalDecaySpeed)
                    {
                        writes.Add((x + y * w, TerrainPixel.DecalLow));
                    }
                    else if (pix == TerrainPixel.DecalLow && random.Next(1000) < Tweaks.World.DecalDecaySpeed)
                    {
                        writes.Add((x + y * w, TerrainPixel.Blank));
                    }
                }
                else if (pix == TerrainPixel.DirtGrow)
                {
                    if (random.Next(1000) < Tweaks.World.DirtRecoverSpeed)
                    {
                        var newPix = random.Next(2) == 0 ? TerrainPixel.DirtHigh : TerrainPixel.DirtLow;
                        writes.Add((x + y * w, newPix));
                    }
                }
            }
        });

        foreach (var writes in stagedWrites.Values)
        {
            foreach (var (offset, value) in writes)
            {
                _terrain.SetPixelRaw(offset, value);
                _terrain.CommitPixel(new Position(offset % w, offset / w));
            }
        }

        stagedWrites.Dispose();
        rng.Dispose();
    }

    private static bool TryQueueDirtRegrow(Random random, int neighbors, int modifier)
    {
        if (neighbors <= 2) return false;
        int chance = Tweaks.World.DirtRegrowSpeed * neighbors * modifier;
        return random.Next(1000) < chance;
    }

    private bool TryQueueDirtRegrow(int x, int y, int neighbors, int modifier, uint salt)
    {
        if (neighbors <= 2) return false;
        int chance = Tweaks.World.DirtRegrowSpeed * neighbors * modifier;
        return Roll1000(x, y, salt) < chance;
    }

    private int Roll1000(int x, int y, uint salt)
    {
        uint mixed = (uint)_simulationSeed
            ^ (uint)_advanceCount * 0x9e3779b9u
            ^ (uint)(x * 73856093)
            ^ (uint)(y * 19349663)
            ^ (salt * 83492791u);
        return (int)(FastRandom.Hash32(mixed) % 1000u);
    }
}

public class SimulationProfile
{
    public TimeSpan Regrow;
    public TimeSpan Projectiles;
    public TimeSpan Tanks;
    public TimeSpan Harvesters;
    public TimeSpan Sprites;
    public TimeSpan Bases;
    public TimeSpan Links;
    public TimeSpan Total;
    public int FrameCount;

    public void Report()
    {
        if (FrameCount == 0) return;
        Console.WriteLine($"[Profile] regrow={Avg(Regrow):F3} bases={Avg(Bases):F3} " +
            $"proj={Avg(Projectiles):F3} tanks={Avg(Tanks):F3} harv={Avg(Harvesters):F3} " +
            $"spr={Avg(Sprites):F3} links={Avg(Links):F3} " +
            $"| total={Avg(Total):F3} ms (avg over {FrameCount} frames)");
        Reset();
    }

    private double Avg(TimeSpan ts) => ts.TotalMilliseconds / FrameCount;

    public void Reset()
    {
        Regrow = Projectiles = Tanks = Harvesters = Sprites = Bases = Links = Total = TimeSpan.Zero;
        FrameCount = 0;
    }
}
