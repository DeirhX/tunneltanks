using Tunnerer.Core;
using Tunnerer.Core.Entities.Links;
using Tunnerer.Core.Entities.Machines;
using Tunnerer.Core.Input;
using Tunnerer.Core.LevelGen;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

public class DeterminismTests
{
    [Fact]
    public void DeterministicMode_SameSeed_SameInputs_IdenticalStateEachFrame()
    {
        const int seed = 4242;
        var w1 = CreateDeterministicWorld(seed, parallelMaterialize: false);
        var w2 = CreateDeterministicWorld(seed, parallelMaterialize: false);

        var script = BuildScript();
        for (int frame = 0; frame < 200; frame++)
        {
            var input = script[frame % script.Length];
            w1.Advance(i => i == 0 ? input : default);
            w2.Advance(i => i == 0 ? input : default);
            Assert.Equal(ComputeWorldStateHash(w1), ComputeWorldStateHash(w2));
        }
    }

    [Fact]
    public void DeterministicMode_IsIndependentFromWallClockDelays()
    {
        const int seed = 31337;
        var fast = CreateDeterministicWorld(seed, parallelMaterialize: false);
        var slow = CreateDeterministicWorld(seed, parallelMaterialize: false);
        SeedLinkAndMachineState(fast);
        SeedLinkAndMachineState(slow);

        for (int frame = 0; frame < 140; frame++)
        {
            fast.Advance(_ => default);
            Thread.Sleep(2);
            slow.Advance(_ => default);
            Assert.Equal(ComputeWorldStateHash(fast), ComputeWorldStateHash(slow));
        }
    }

    [Fact]
    public void DeterministicMode_LinksAndMachines_AreStableAcrossRuns()
    {
        const int seed = 9001;
        var w1 = CreateDeterministicWorld(seed, parallelMaterialize: false);
        var w2 = CreateDeterministicWorld(seed, parallelMaterialize: false);
        SeedLinkAndMachineState(w1);
        SeedLinkAndMachineState(w2);

        for (int frame = 0; frame < 100; frame++)
        {
            w1.Advance(_ => default);
            w2.Advance(_ => default);
            Assert.Equal(ComputeWorldStateHash(w1), ComputeWorldStateHash(w2));
        }
    }

    [Fact]
    public void DeterministicMode_DifferentSeeds_Diverge()
    {
        var w1 = CreateDeterministicWorld(1001, parallelMaterialize: false);
        var w2 = CreateDeterministicWorld(1002, parallelMaterialize: false);

        bool diverged = false;
        for (int frame = 0; frame < 80; frame++)
        {
            w1.Advance(_ => default);
            w2.Advance(_ => default);
            if (ComputeWorldStateHash(w1) != ComputeWorldStateHash(w2))
            {
                diverged = true;
                break;
            }
        }
        Assert.True(diverged, "Different seeds should produce diverging simulation state.");
    }

    [Fact]
    public void DeterministicMode_ParallelMaterialize_IsStableAcrossRuns()
    {
        const int seed = 777;
        var w1 = CreateDeterministicWorld(seed, parallelMaterialize: true);
        var w2 = CreateDeterministicWorld(seed, parallelMaterialize: true);

        for (int frame = 0; frame < 60; frame++)
        {
            w1.Advance(_ => default);
            w2.Advance(_ => default);
            Assert.Equal(ComputeWorldStateHash(w1), ComputeWorldStateHash(w2));
        }
    }

    [Fact]
    public void SeededGeneration_OptimizedMode_ProducesIdenticalMap()
    {
        const int seed = 2027;
        var size = TestHelpers.DefaultMapSize;
        var gen = new ToastGenerator();

        var (t1, s1) = gen.Generate(size, seed: seed, mode: LevelGenMode.Optimized);
        var (t2, s2) = gen.Generate(size, seed: seed, mode: LevelGenMode.Optimized);

        Assert.Equal(s1.Length, s2.Length);
        for (int i = 0; i < s1.Length; i++)
            Assert.Equal(s1[i], s2[i]);

        for (int i = 0; i < size.Area; i++)
            Assert.Equal(t1[i], t2[i]);
    }

    private static World CreateDeterministicWorld(int seed, bool parallelMaterialize)
    {
        var mapSize = TestHelpers.DefaultMapSize;
        var gen = new ToastGenerator();
        var (terrain, spawns) = gen.Generate(mapSize, seed: seed);
        var world = new World(mapSize, deterministicSimulation: true, simulationSeed: seed);
        world.Initialize(terrain, spawns, materializeSeed: seed + 1, parallelMaterialize: parallelMaterialize);
        return world;
    }

    private static ControllerOutput[] BuildScript()
    {
        var script = new List<ControllerOutput>();
        for (int i = 0; i < 16; i++)
            script.Add(new ControllerOutput { MoveSpeed = new Offset(1, 0) });
        for (int i = 0; i < 10; i++)
            script.Add(new ControllerOutput { MoveSpeed = new Offset(0, -1) });
        for (int i = 0; i < 8; i++)
            script.Add(new ControllerOutput { ShootPrimary = true, AimDirection = new DirectionF(1, 0) });
        for (int i = 0; i < 8; i++)
            script.Add(default);
        return script.ToArray();
    }

    private static void SeedLinkAndMachineState(World world)
    {
        int cx = world.Terrain.Width / 2;
        int cy = world.Terrain.Height / 2;
        world.LinkMap.RegisterPoint(new Position(cx - 6, cy), LinkPointType.Base);
        world.LinkMap.RegisterPoint(new Position(cx + 6, cy), LinkPointType.Machine);

        var machine = new Machine(new Position(cx, cy + 5), MachineType.Harvester, ownerColor: 0)
        {
            State = MachineState.Planted,
        };
        world.Machines.Add(machine);
    }

    private static ulong ComputeWorldStateHash(World world)
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;

        ulong Mix(ulong h, uint value)
        {
            h ^= value;
            h *= prime;
            return h;
        }

        var terrain = world.Terrain.Data;
        for (int i = 0; i < terrain.Length; i++)
            hash = Mix(hash, (uint)terrain[i]);

        hash = Mix(hash, (uint)world.AdvanceCount);
        hash = Mix(hash, (uint)world.Projectiles.Count);
        hash = Mix(hash, (uint)world.Machines.Machines.Count);
        hash = Mix(hash, (uint)world.LinkMap.Points.Count);
        hash = Mix(hash, (uint)world.LinkMap.Links.Count);

        var tanks = world.TankList.Tanks;
        hash = Mix(hash, (uint)tanks.Count);
        for (int i = 0; i < tanks.Count; i++)
        {
            var t = tanks[i];
            hash = Mix(hash, (uint)t.Position.X);
            hash = Mix(hash, (uint)t.Position.Y);
            hash = Mix(hash, (uint)t.Direction);
            hash = Mix(hash, (uint)t.LivesLeft);
            hash = Mix(hash, (uint)(int)t.Reactor.Heat);
            hash = Mix(hash, (uint)(int)t.Reactor.Health);
            hash = Mix(hash, (uint)t.Resources.Dirt);
            hash = Mix(hash, (uint)t.Resources.Minerals);
            hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(t.Heat));
            hash = Mix(hash, t.IsDead ? 1u : 0u);
        }

        var machines = world.Machines.Machines;
        for (int i = 0; i < machines.Count; i++)
        {
            var m = machines[i];
            hash = Mix(hash, (uint)m.Position.X);
            hash = Mix(hash, (uint)m.Position.Y);
            hash = Mix(hash, (uint)m.Type);
            hash = Mix(hash, (uint)m.State);
            hash = Mix(hash, (uint)m.OwnerColor);
            hash = Mix(hash, m.IsAlive ? 1u : 0u);
            hash = Mix(hash, (uint)(int)m.Reactor.Heat);
            hash = Mix(hash, (uint)(int)m.Reactor.Health);
        }

        var points = world.LinkMap.Points;
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            hash = Mix(hash, (uint)p.Id);
            hash = Mix(hash, (uint)p.Position.X);
            hash = Mix(hash, (uint)p.Position.Y);
            hash = Mix(hash, (uint)p.Type);
            hash = Mix(hash, p.IsEnabled ? 1u : 0u);
            hash = Mix(hash, p.IsPowered ? 1u : 0u);
        }

        var links = world.LinkMap.Links;
        for (int i = 0; i < links.Count; i++)
        {
            var l = links[i];
            hash = Mix(hash, (uint)l.From.Id);
            hash = Mix(hash, (uint)l.To.Id);
            hash = Mix(hash, (uint)l.Type);
            hash = Mix(hash, l.IsAlive ? 1u : 0u);
        }

        int w = world.Terrain.Width, h = world.Terrain.Height;
        var pix = new uint[w * h];
        var surface = new Surface(pix, w, h);
        world.LinkMap.Draw(surface);
        world.Machines.Draw(surface);
        world.Projectiles.Draw(surface);
        world.Sprites.Draw(surface);
        world.TankList.Draw(surface);
        for (int i = 0; i < pix.Length; i++)
            hash = Mix(hash, pix[i]);

        return hash;
    }
}
