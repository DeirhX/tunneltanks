using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Input;
using Tunnerer.Core.LevelGen;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

/// <summary>
/// Deterministic replay tests that use fixed seeds and scripted inputs
/// to verify simulation produces identical results across runs.
/// </summary>
public class ReplayTests
{
    private static World CreateSeededWorld() => TestHelpers.CreateSeededWorld();

    [Fact]
    public void SeededGeneration_ProducesIdenticalMap()
    {
        var gen = new ToastGenerator();
        var size = TestHelpers.DefaultMapSize;
        int seed = TestHelpers.DefaultSeed;
        var (t1, s1) = gen.Generate(size, seed: seed);
        var (t2, s2) = gen.Generate(size, seed: seed);

        Assert.Equal(s1.Length, s2.Length);
        for (int i = 0; i < s1.Length; i++)
            Assert.Equal(s1[i], s2[i]);

        for (int i = 0; i < size.Area; i++)
            Assert.Equal(t1[i], t2[i]);
    }

    [Fact]
    public void SeededMaterialize_ProducesIdenticalTerrain()
    {
        var gen = new ToastGenerator();
        var size = TestHelpers.DefaultMapSize;
        int seed = TestHelpers.DefaultSeed;
        var (t1, _) = gen.Generate(size, seed: seed);
        var (t2, _) = gen.Generate(size, seed: seed);
        t1.MaterializeTerrain(seed: seed + 1);
        t2.MaterializeTerrain(seed: seed + 1);

        for (int i = 0; i < size.Area; i++)
            Assert.Equal(t1[i], t2[i]);
    }

    [Fact]
    public void SeededWorld_SpawnPositions_AreStable()
    {
        var w1 = CreateSeededWorld();
        var w2 = CreateSeededWorld();

        Assert.Equal(w1.TankList.Tanks.Count, w2.TankList.Tanks.Count);
        for (int i = 0; i < w1.TankList.Tanks.Count; i++)
            Assert.Equal(w1.TankList.Tanks[i].Position, w2.TankList.Tanks[i].Position);
    }

    [Fact]
    public void ScriptedReplay_MovingRight_TankAdvances()
    {
        var world = CreateSeededWorld();
        var startPos = world.TankList.Tanks[0].Position;

        var moveRight = new ControllerOutput { MoveSpeed = new Offset(1, 0) };
        for (int i = 0; i < 50; i++)
            world.Advance(_ => moveRight);

        var endPos = world.TankList.Tanks[0].Position;
        Assert.True(endPos.X > startPos.X,
            $"Tank should have moved right: start={startPos}, end={endPos}");
    }

    [Fact]
    public void ScriptedReplay_Shooting_CreatesProjectiles()
    {
        var world = CreateSeededWorld();

        // Move out of base first (need to be in open area to shoot)
        var moveUp = new ControllerOutput { MoveSpeed = new Offset(0, -1) };
        for (int i = 0; i < 30; i++)
            world.Advance(_ => moveUp);

        int projectilesBefore = world.Projectiles.Count;

        var shootRight = new ControllerOutput
        {
            MoveSpeed = new Offset(0, 0),
            ShootPrimary = true,
            AimDirection = new DirectionF(1, 0),
        };
        for (int i = 0; i < 10; i++)
            world.Advance(_ => shootRight);

        Assert.True(world.Projectiles.Count > projectilesBefore,
            $"Expected projectiles to appear: before={projectilesBefore}, after={world.Projectiles.Count}");
    }

    [Fact]
    public void ScriptedReplay_TwoRuns_IdenticalPositions()
    {
        var script = BuildScript();

        var w1 = CreateSeededWorld();
        var w2 = CreateSeededWorld();

        for (int frame = 0; frame < script.Length; frame++)
        {
            var input = script[frame];
            w1.Advance(i => i == 0 ? input : default);
            w2.Advance(i => i == 0 ? input : default);
        }

        Assert.Equal(w1.TankList.Tanks[0].Position, w2.TankList.Tanks[0].Position);
        Assert.Equal(w1.TankList.Tanks[1].Position, w2.TankList.Tanks[1].Position);
        Assert.Equal(w1.AdvanceCount, w2.AdvanceCount);
    }

    [Fact]
    public void ScriptedReplay_TerrainDigging_RemovesDirt()
    {
        var world = CreateSeededWorld();
        var tank = world.TankList.Tanks[0];
        var startPos = tank.Position;

        // Move up out of the base (base has blank space), then count dirt ahead
        var moveUp = new ControllerOutput { MoveSpeed = new Offset(0, -1) };
        for (int i = 0; i < 60; i++)
            world.Advance(_ => moveUp);

        // The tank should have dug through some dirt
        bool anyBlank = false;
        int scanY = startPos.Y - 1;
        for (int dy = 0; dy < 30; dy++)
        {
            var pos = new Position(startPos.X, scanY - dy);
            if (!world.Terrain.IsInside(pos)) break;
            if (world.Terrain.GetPixelRaw(pos) == TerrainPixel.Blank)
            { anyBlank = true; break; }
        }
        Assert.True(anyBlank, "Tank should have dug some blank pixels above its starting base");
    }

    [Fact]
    public void ScriptedReplay_ComplexSequence_MatchesCheckpoints()
    {
        var world = CreateSeededWorld();

        // Phase 1: Move right for 20 frames
        for (int i = 0; i < 20; i++)
            world.Advance(_ => new ControllerOutput { MoveSpeed = new Offset(1, 0) });
        var checkpoint1 = world.TankList.Tanks[0].Position;

        // Phase 2: Move up for 20 frames
        for (int i = 0; i < 20; i++)
            world.Advance(_ => new ControllerOutput { MoveSpeed = new Offset(0, -1) });
        var checkpoint2 = world.TankList.Tanks[0].Position;

        // Phase 3: Shoot while stationary for 10 frames
        for (int i = 0; i < 10; i++)
            world.Advance(_ => new ControllerOutput
            {
                ShootPrimary = true,
                AimDirection = new DirectionF(1, 0),
            });
        var checkpoint3 = world.TankList.Tanks[0].Position;

        // Verify reproducibility: run same sequence again
        var world2 = CreateSeededWorld();
        for (int i = 0; i < 20; i++)
            world2.Advance(_ => new ControllerOutput { MoveSpeed = new Offset(1, 0) });
        Assert.Equal(checkpoint1, world2.TankList.Tanks[0].Position);

        for (int i = 0; i < 20; i++)
            world2.Advance(_ => new ControllerOutput { MoveSpeed = new Offset(0, -1) });
        Assert.Equal(checkpoint2, world2.TankList.Tanks[0].Position);

        for (int i = 0; i < 10; i++)
            world2.Advance(_ => new ControllerOutput
            {
                ShootPrimary = true,
                AimDirection = new DirectionF(1, 0),
            });
        Assert.Equal(checkpoint3, world2.TankList.Tanks[0].Position);
    }

    [Fact]
    public void ScriptedReplay_P2TwitchAI_DeterministicWithSeed()
    {
        var world = CreateSeededWorld();
        var ai1 = new TwitchAI(seed: 99);
        var ai2 = new TwitchAI(seed: 99);

        for (int i = 0; i < 50; i++)
        {
            var input1 = ai1.GetInput(world.TankList.Tanks[1]);
            var input2 = ai2.GetInput(world.TankList.Tanks[1]);
            Assert.Equal(input1.MoveSpeed, input2.MoveSpeed);
            Assert.Equal(input1.ShootPrimary, input2.ShootPrimary);
        }
    }

    private static ControllerOutput[] BuildScript()
    {
        var script = new List<ControllerOutput>();
        // 20 frames: move right
        for (int i = 0; i < 20; i++)
            script.Add(new ControllerOutput { MoveSpeed = new Offset(1, 0) });
        // 10 frames: move up
        for (int i = 0; i < 10; i++)
            script.Add(new ControllerOutput { MoveSpeed = new Offset(0, -1) });
        // 5 frames: shoot right
        for (int i = 0; i < 5; i++)
            script.Add(new ControllerOutput { ShootPrimary = true, AimDirection = new DirectionF(1, 0) });
        // 15 frames: idle
        for (int i = 0; i < 15; i++)
            script.Add(default);
        return script.ToArray();
    }
}
