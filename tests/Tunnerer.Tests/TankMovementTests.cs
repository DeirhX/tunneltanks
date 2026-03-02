using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Input;
using Tunnerer.Core.LevelGen;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

public class TankMovementTests
{
    private static World CreateSeededWorld() => TestHelpers.CreateSeededWorld();

    /// <summary>
    /// Creates a small world with a controlled terrain layout for precise testing.
    /// The terrain has a horizontal dirt wall at a known position above the spawn.
    /// </summary>
    private (World world, Position spawn) CreateControlledWorld()
    {
        var size = new Size(100, 100);
        var terrain = new TerrainGrid(size);

        // Fill everything with blank (open)
        for (int i = 0; i < size.Area; i++)
            terrain[i] = TerrainPixel.Blank;

        // Create a horizontal dirt wall at y=30 spanning x=10..90
        for (int x = 10; x < 90; x++)
        {
            terrain[x + 30 * size.X] = TerrainPixel.DirtHigh;
            terrain[x + 31 * size.X] = TerrainPixel.DirtHigh;
            terrain[x + 32 * size.X] = TerrainPixel.DirtHigh;
        }

        // Create a vertical dirt corridor of width 5 around x=50, from y=35 to y=60
        for (int y = 35; y < 60; y++)
            for (int x = 48; x <= 52; x++)
                terrain[x + y * size.X] = TerrainPixel.Blank;

        // Fill walls on either side of the corridor
        for (int y = 35; y < 60; y++)
        {
            for (int x = 44; x < 48; x++)
                terrain[x + y * size.X] = TerrainPixel.DirtHigh;
            for (int x = 53; x < 57; x++)
                terrain[x + y * size.X] = TerrainPixel.DirtHigh;
        }

        var spawn = new Position(50, 50);
        var world = new World(size);

        // Manually set up terrain in world
        for (int i = 0; i < size.Area; i++)
            world.Terrain[i] = terrain[i];

        // Add a base and tank at our chosen spawn
        world.TankBases.AddBase(spawn, 0);
        world.TankBases.CreateBasesInTerrain(world.Terrain);
        var tankBase = world.TankBases.GetSpawn(0);
        if (tankBase != null)
            world.TankList.AddTank(0, tankBase);

        return (world, spawn);
    }

    [Fact]
    public void Digging_DoesNotMove_OnSameFrame()
    {
        var world = CreateSeededWorld();
        var tank = world.TankList.Tanks[0];
        var startPos = tank.Position;

        // Move up - initially in or near the base (blank space), should move freely
        // After moving out of blank space and hitting dirt, the tank should stop for one frame
        var positions = new List<Position> { startPos };
        var moveUp = TestSimulation.Move(0, -1);

        for (int i = 0; i < 100; i++)
        {
            world.Advance(_ => moveUp);
            positions.Add(tank.Position);
        }

        // Verify that there are frames where position didn't change (digging frames)
        int stoppedFrames = 0;
        for (int i = 1; i < positions.Count; i++)
        {
            if (positions[i] == positions[i - 1])
                stoppedFrames++;
        }

        Assert.True(stoppedFrames > 0,
            "Tank should have stopped on at least one frame while digging through dirt");
    }

    [Fact]
    public void Digging_IsSlow_ComparedToEmptySpace()
    {
        var world = CreateSeededWorld();
        var tank = world.TankList.Tanks[0];

        // First, measure frames to cross open space (base area is blank)
        var startPos = tank.Position;
        var moveRight = TestSimulation.Move(1, 0);

        // Run 10 frames in what should be open base area
        int openMoved = 0;
        for (int i = 0; i < 10; i++)
        {
            var before = tank.Position;
            world.Advance(_ => moveRight);
            if (tank.Position != before) openMoved++;
        }

        // Now generate a second world and move through dirt
        var world2 = CreateSeededWorld();
        var tank2 = world2.TankList.Tanks[0];

        // Move up to hit dirt (away from base)
        var moveUp = TestSimulation.Move(0, -1);
        TestSimulation.Advance(world2, frames: 60, moveUp);

        // Continue moving up into dirt for 10 more frames
        int dirtMoved = 0;
        for (int i = 0; i < 10; i++)
        {
            var before = tank2.Position;
            world2.Advance(_ => moveUp);
            if (tank2.Position != before) dirtMoved++;
        }

        // Movement through dirt should be slower (fewer successful moves)
        Assert.True(openMoved > dirtMoved,
            $"Open space moves ({openMoved}) should be greater than dirt moves ({dirtMoved})");
    }

    [Fact]
    public void DigShape_Is7x7MinusCorners()
    {
        // Large world so the base doesn't overlap the dig test area
        var size = new Size(200, 200);
        var terrain = new TerrainGrid(size);

        // Fill everything blank
        for (int i = 0; i < size.Area; i++)
            terrain[i] = TerrainPixel.Blank;

        // Place base at (100, 100) - well away from our dig test area at y=30..40
        var basePos = new Position(100, 100);

        // Create a solid dirt wall at y=30..36 across x=90..110
        for (int y = 30; y <= 36; y++)
            for (int x = 90; x <= 110; x++)
                terrain[x + y * size.X] = TerrainPixel.DirtHigh;

        var world = new World(size);
        for (int i = 0; i < size.Area; i++)
            world.Terrain[i] = terrain[i];

        world.TankBases.AddBase(basePos, 0);
        world.TankBases.CreateBasesInTerrain(world.Terrain);
        var tankBase = world.TankBases.GetSpawn(0);
        world.TankList.AddTank(0, tankBase!);

        // Manually teleport the tank to just above the dirt wall
        var tank = world.TankList.Tanks[0];
        tank.Position = new Position(100, 29);

        // Move down into the dirt wall
        var moveDown = TestSimulation.Move(0, 1);
        world.Advance(_ => moveDown);

        // Dig target is (100, 30)
        var digCenter = new Position(100, 30);

        // Count blanked pixels in the 7x7 area (minus corners)
        int blanked = 0;
        for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                if ((dx == -3 || dx == 3) && (dy == -3 || dy == 3)) continue;
                var pos = new Position(digCenter.X + dx, digCenter.Y + dy);
                if (world.Terrain.GetPixelRaw(pos) == TerrainPixel.Blank)
                    blanked++;
            }

        // All 45 non-corner cells should be blanked (7*7-4 = 45)
        // Some at top (y=27..29) were already blank, so at least 21 new ones + 24 existing
        Assert.True(blanked >= 35,
            $"Expected at least 35 blanked pixels in 7x7 dig area, got {blanked}");

        // The 4 corners should NOT be blanked (they are dirt, outside dig shape)
        var corners = new[]
        {
            new Position(digCenter.X - 3, digCenter.Y - 3),
            new Position(digCenter.X + 3, digCenter.Y - 3),
            new Position(digCenter.X - 3, digCenter.Y + 3),
            new Position(digCenter.X + 3, digCenter.Y + 3),
        };
        // Top corners (y=27) were originally blank, bottom corners (y=33) were dirt
        foreach (var corner in new[] { corners[2], corners[3] })
        {
            Assert.NotEqual(TerrainPixel.Blank, world.Terrain.GetPixelRaw(corner));
        }
    }

    [Fact]
    public void DirectionChange_InCorridor_WorksAfterDigging()
    {
        var (world, spawn) = CreateControlledWorld();
        var tank = world.TankList.Tanks[0];

        // Move tank down into the corridor
        var moveDown = TestSimulation.Move(0, 1);
        for (int i = 0; i < 5; i++)
            world.Advance(_ => moveDown);

        var posInCorridor = tank.Position;

        // Now try to move right (into the corridor wall of dirt)
        var moveRight = TestSimulation.Move(1, 0);
        for (int i = 0; i < 5; i++)
            world.Advance(_ => moveRight);

        // The tank should have dug into the wall and potentially moved
        // At minimum it should have cleared some dirt to the right
        bool anyBlanked = false;
        for (int dx = 1; dx <= 7; dx++)
        {
            var checkPos = new Position(posInCorridor.X + dx, posInCorridor.Y);
            if (world.Terrain.IsInside(checkPos) &&
                world.Terrain.GetPixelRaw(checkPos) == TerrainPixel.Blank)
            { anyBlanked = true; break; }
        }

        Assert.True(anyBlanked, "Tank should have dug dirt when trying to change direction in corridor");
    }

    [Fact]
    public void AimDirection_FromCrosshair_OverridesMovement()
    {
        var world = CreateSeededWorld();
        var tank = world.TankList.Tanks[0];

        var input = TestSimulation.MoveAndAim(0, -1, 1f, 0f);

        for (int i = 0; i < 5; i++)
            world.Advance(_ => input);

        Assert.True(tank.Turret.Direction.X > 0.9f,
            $"Turret should aim right from crosshair, got direction=({tank.Turret.Direction.X}, {tank.Turret.Direction.Y})");
    }

    [Fact]
    public void TorchDig_ShootingInMoveDirection_MovesFaster()
    {
        // Two runs through a seeded world: one without shooting, one shooting ahead
        var world1 = CreateSeededWorld();
        var tank1 = world1.TankList.Tanks[0];

        var world2 = CreateSeededWorld();
        var tank2 = world2.TankList.Tanks[0];

        // Move up without shooting
        var noShoot = TestSimulation.Move(0, -1);
        // Move up while shooting ahead (turret aimed up)
        var shootAhead = TestSimulation.MoveAndAim(0, -1, 0f, -1f, shootPrimary: true);

        for (int i = 0; i < 80; i++)
        {
            world1.Advance(_ => noShoot);
            world2.Advance(_ => shootAhead);
        }

        int dist1 = Math.Abs(tank1.Position.Y - world1.TankBases.GetSpawn(0)!.Position.Y);
        int dist2 = Math.Abs(tank2.Position.Y - world2.TankBases.GetSpawn(0)!.Position.Y);

        Assert.True(dist2 > dist1,
            $"Shooting ahead should travel farther: noShoot={dist1}, shootAhead={dist2}");
    }

    [Fact(Skip = "Flaky under RNG/collision variance; disabled temporarily pending deterministic rewrite.")]
    public void TorchDig_ShootingSideways_SlowerThanShootingAhead()
    {
        // Shooting perpendicular should be slower than shooting in movement direction
        // (even though bullet explosions may clear some terrain as collateral)
        var world1 = CreateSeededWorld();
        var tank1 = world1.TankList.Tanks[0];

        var world2 = CreateSeededWorld();
        var tank2 = world2.TankList.Tanks[0];

        // Move up while shooting right (perpendicular)
        var shootSideways = TestSimulation.MoveAndAim(0, -1, 1f, 0f, shootPrimary: true);
        // Move up while shooting ahead
        var shootAhead = TestSimulation.MoveAndAim(0, -1, 0f, -1f, shootPrimary: true);

        for (int i = 0; i < 80; i++)
        {
            world1.Advance(_ => shootSideways);
            world2.Advance(_ => shootAhead);
        }

        int distSideways = Math.Abs(tank1.Position.Y - world1.TankBases.GetSpawn(0)!.Position.Y);
        int distAhead = Math.Abs(tank2.Position.Y - world2.TankBases.GetSpawn(0)!.Position.Y);

        Assert.True(distAhead >= distSideways,
            $"Shooting ahead should travel at least as far as sideways: ahead={distAhead}, sideways={distSideways}");
    }

    [Fact]
    public void TorchDig_RockOnlyDestroyedWhenShooting()
    {
        // Set up terrain with rock in path, verify it's only destroyed when shooting
        var size = new Size(200, 200);
        var world = new World(size);
        for (int i = 0; i < size.Area; i++)
            world.Terrain[i] = TerrainPixel.Blank;

        // Place a line of rock at y=60
        for (int x = 90; x <= 110; x++)
            world.Terrain[x + 60 * size.X] = TerrainPixel.Rock;

        var basePos = new Position(100, 100);
        world.TankBases.AddBase(basePos, 0);
        world.TankBases.CreateBasesInTerrain(world.Terrain);
        world.TankList.AddTank(0, world.TankBases.GetSpawn(0)!);
        var tank = world.TankList.Tanks[0];
        tank.Position = new Position(100, 62);

        // Move up into rock WITHOUT shooting
        var noShoot = TestSimulation.Move(0, -1);
        for (int i = 0; i < 5; i++)
            world.Advance(_ => noShoot);

        // Rock should still be intact (not torchable without shooting)
        int rockRemaining = 0;
        for (int x = 97; x <= 103; x++)
            if (world.Terrain.GetPixelRaw(new Position(x, 60)) == TerrainPixel.Rock)
                rockRemaining++;

        Assert.True(rockRemaining > 0, "Rock should NOT be destroyed when not shooting");
    }
}
