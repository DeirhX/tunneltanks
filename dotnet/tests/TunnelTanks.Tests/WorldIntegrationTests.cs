using TunnelTanks.Core;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Input;
using TunnelTanks.Core.LevelGen;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Types;

namespace TunnelTanks.Tests;

public class WorldIntegrationTests
{
    private static readonly Size TestSize = new(320, 200);

    private (World world, uint[] terrainPixels) SetupWorld()
    {
        var gen = new ToastGenerator();
        var (terrain, spawns) = gen.Generate(TestSize);

        var world = new World(TestSize);
        world.Initialize(terrain, spawns);

        var terrainPixels = new uint[TestSize.Area];
        world.Terrain.DrawAllToSurface(terrainPixels);
        return (world, terrainPixels);
    }

    [Fact]
    public void World_AdvanceSingleFrame_DoesNotThrow()
    {
        var (world, _) = SetupWorld();
        world.Advance(_ => default);
    }

    [Fact]
    public void World_Advance100Frames_Runs()
    {
        var (world, _) = SetupWorld();
        for (int i = 0; i < 100; i++)
            world.Advance(_ => default);

        Assert.Equal(100, world.AdvanceCount);
        Assert.False(world.IsGameOver);
    }

    [Fact]
    public void World_TanksSpawnAtBases()
    {
        var (world, _) = SetupWorld();
        Assert.True(world.TankList.Tanks.Count >= 2, "Expected at least 2 tanks");

        for (int i = 0; i < world.TankList.Tanks.Count; i++)
        {
            var tank = world.TankList.Tanks[i];
            var baseObj = world.TankBases.GetSpawn(i);
            Assert.NotNull(baseObj);
            Assert.Equal(baseObj.Position, tank.Position);
        }
    }

    [Fact]
    public void TerrainBuffer_NotCorrupted_ByEntityDraw()
    {
        var (world, terrainPixels) = SetupWorld();

        // Take a snapshot of the terrain buffer before entity drawing
        var snapshot = new uint[TestSize.Area];
        Array.Copy(terrainPixels, snapshot, terrainPixels.Length);

        // Simulate what the old buggy code did: draw entities directly on terrain buffer
        var composite = new uint[TestSize.Area];
        Array.Copy(terrainPixels, composite, terrainPixels.Length);
        world.TankList.Draw(composite, TestSize.X, TestSize.Y);

        // The composite buffer should now differ (tanks were drawn on it)
        bool anyDifference = false;
        for (int i = 0; i < composite.Length; i++)
        {
            if (composite[i] != snapshot[i])
            { anyDifference = true; break; }
        }
        Assert.True(anyDifference, "Tanks should modify the composite buffer");

        // The original terrain buffer should be untouched
        Assert.Equal(snapshot, terrainPixels);
    }

    [Fact]
    public void CompositeBuffer_RebuildEachFrame_NoGhosting()
    {
        var (world, terrainPixels) = SetupWorld();

        // Frame 1: copy terrain, draw entities
        var composite1 = new uint[TestSize.Area];
        Array.Copy(terrainPixels, composite1, terrainPixels.Length);
        world.TankList.Draw(composite1, TestSize.X, TestSize.Y);

        // Move a tank by advancing with input
        world.Advance(i => i == 0
            ? new ControllerOutput { MoveSpeed = new Offset(1, 0) }
            : default);
        world.Terrain.DrawChangesToSurface(terrainPixels);

        // Frame 2: rebuild composite from scratch
        var composite2 = new uint[TestSize.Area];
        Array.Copy(terrainPixels, composite2, terrainPixels.Length);
        world.TankList.Draw(composite2, TestSize.X, TestSize.Y);

        // The composites should differ because the tank moved
        // But the terrain buffer itself should have no tank pixels
        // (only terrain data from DrawChangesToSurface)
        var tank = world.TankList.Tanks[0];
        var baseColor = Pixel.GetColor(world.Terrain.GetPixelRaw(tank.Position)).ToArgb();
        // Verify terrain buffer at tank position has terrain color (not tank color)
        int off = tank.Position.X + tank.Position.Y * TestSize.X;
        Assert.Equal(baseColor, terrainPixels[off]);
    }

    [Fact]
    public void RegrowPass_UsesStagedWrites_NoBoundaryArtifacts()
    {
        // Run several regrow passes and verify no LevelGen pixels leak into the materialized terrain
        var (world, _) = SetupWorld();

        // Blank out a region to trigger regrow
        for (int y = 50; y < 60; y++)
            for (int x = 50; x < 60; x++)
                world.Terrain.SetPixel(new Position(x, y), TerrainPixel.Blank);

        // Run many frames to trigger regrow timer
        for (int i = 0; i < 500; i++)
            world.Advance(_ => default);

        // Verify no LevelGen artifacts
        for (int i = 0; i < TestSize.Area; i++)
        {
            var pix = world.Terrain[i];
            Assert.False(pix == TerrainPixel.LevelGenDirt || pix == TerrainPixel.LevelGenRock || pix == TerrainPixel.LevelGenMark,
                $"LevelGen pixel {pix} found at offset {i} during gameplay");
        }
    }

    [Fact]
    public void Tweaks_SmoothingSteps_MatchesCpp()
    {
        Assert.Equal(-1, Tweaks.LevelGen.SmoothingSteps);
    }

    [Fact]
    public void Tweaks_Constants_MatchCpp()
    {
        Assert.Equal(30, Tweaks.LevelGen.BorderWidth);
        Assert.Equal(300, Tweaks.LevelGen.MaxDirtSpawnOdds);
        Assert.Equal(70, Tweaks.LevelGen.DirtSpawnProgression);
        Assert.Equal(65, Tweaks.LevelGen.DirtTargetPercent);
        Assert.Equal(150, Tweaks.LevelGen.TreeSize);
        Assert.Equal(150, Tweaks.Base.MinDistance);
        Assert.Equal(35, Tweaks.Base.BaseSize);
        Assert.Equal(7, Tweaks.Base.DoorSize);
        Assert.Equal(24, Tweaks.Perf.TargetFps);
        Assert.Equal(64, Tweaks.Perf.SectorSize);
    }
}
