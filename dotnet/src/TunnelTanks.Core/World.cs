namespace TunnelTanks.Core;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Entities;
using TunnelTanks.Core.Collision;
using TunnelTanks.Core.Input;
using TunnelTanks.Core.Entities.Projectiles;
using TunnelTanks.Core.Entities.Machines;
using TunnelTanks.Core.Entities.Links;
using System.Diagnostics;

public class World
{
    private readonly TerrainGrid _terrain;
    private readonly TankBases _tankBases = new();
    private readonly TankList _tankList = new();
    private readonly ProjectileList _projectiles = new();
    private readonly MachineList _machines = new();
    private readonly LinkMap _linkMap = new();
    private readonly SpriteList _sprites = new();
    private readonly WorldSectors _sectors;

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
    public WorldSectors Sectors => _sectors;
    public int AdvanceCount => _advanceCount;
    public bool IsGameOver => _gameOver;

    public SimulationProfile Profile { get; } = new();

    public World(Size terrainSize)
    {
        _terrain = new TerrainGrid(terrainSize);
        _sectors = new WorldSectors(terrainSize);
        _regrowTimer.Start();
    }

    public void Initialize(TerrainGrid generatedTerrain, Position[] spawns,
        int? materializeSeed = null, bool parallelMaterialize = false)
    {
        for (int i = 0; i < generatedTerrain.Size.Area; i++)
            _terrain[i] = generatedTerrain[i];

        _terrain.MaterializeTerrain(materializeSeed, parallel: parallelMaterialize);

        for (int i = 0; i < spawns.Length; i++)
            _tankBases.AddBase(spawns[i], i);

        _tankBases.CreateBasesInTerrain(_terrain);

        for (int i = 0; i < spawns.Length; i++)
        {
            var tankBase = _tankBases.GetSpawn(i);
            if (tankBase != null)
                _tankList.AddTank(i, tankBase);
        }
    }

    public void Advance(Func<int, ControllerOutput> getInput)
    {
        if (_gameOver) return;

        var frameWatch = Stopwatch.StartNew();

        _advanceCount++;
        _elapsed += Tweaks.World.AdvanceStep;

        ProfileSection(ref Profile.Regrow, RegrowPass);
        ProfileSection(ref Profile.Projectiles, () => _projectiles.Advance(_terrain, _tankList));
        ProfileSection(ref Profile.Tanks, () => _tankList.Advance(this, getInput));
        ProfileSection(ref Profile.Harvesters, () => _machines.Advance(_terrain, Tweaks.World.AdvanceStep));
        ProfileSection(ref Profile.Sprites, () => _sprites.Advance(Tweaks.World.AdvanceStep));
        ProfileSection(ref Profile.Bases, () => _tankBases.Advance());
        ProfileSection(ref Profile.Links, () => _linkMap.Advance(_terrain));

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
        if (_regrowTimer.Elapsed < _regrowAccumulator + Tweaks.World.DirtRecoverInterval)
            return;
        _regrowAccumulator += Tweaks.World.DirtRecoverInterval;

        int w = _terrain.Width, h = _terrain.Height;
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

                if (pix == TerrainPixel.Blank || Pixel.IsScorched(pix))
                {
                    int neighbors = _terrain.CountDirtNeighbors(pos);
                    int modifier = pix == TerrainPixel.Blank
                        ? Tweaks.World.DirtRegrowBlankModifier
                        : Tweaks.World.DirtRegrowScorchedModifier;
                    if (neighbors > 2 && random.Next(1000) < Tweaks.World.DirtRegrowSpeed * neighbors * modifier)
                    {
                        writes.Add((x + y * w, TerrainPixel.DirtGrow));
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
