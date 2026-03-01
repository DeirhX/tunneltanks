using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Resources;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

public class TankHeatBehaviorTests
{
    [Fact]
    public void TankHeat_PassivelyCools_WhenNoHeatSource()
    {
        var world = TestHelpers.CreateSeededWorld();
        var tank = world.TankList.Tanks[0];
        var pos = new Position(world.Terrain.Width / 2, world.Terrain.Height / 2);

        tank.Position = pos;
        ClearArea(world.Terrain, pos, radius: 6);
        tank.Heat = 60f;
        tank.Reactor.Current.Heat = new Heat(60);

        for (int i = 0; i < 20; i++)
            tank.Advance(world, default);

        Assert.True(tank.Heat < 60f, $"Expected passive cooling, but heat stayed at {tank.Heat:0.00}");
    }

    [Fact]
    public void TankHeat_OutsideBase_DriftsTowardAmbientTwenty()
    {
        var world = TestHelpers.CreateSeededWorld(seed: 2222);
        var tank = world.TankList.Tanks[0];
        var pos = new Position(world.Terrain.Width / 2, world.Terrain.Height / 2);

        tank.Position = pos;
        ClearArea(world.Terrain, pos, radius: 8);
        tank.Heat = 0f;
        tank.Reactor.Current.Heat = new Heat(0);

        for (int i = 0; i < 120; i++)
            tank.Advance(world, default);

        Assert.InRange(tank.Heat, 16f, 24f);
    }

    [Fact]
    public void TankHeat_HotTerrainTransfer_DependsOnHeatDelta()
    {
        var worldCoolTank = TestHelpers.CreateSeededWorld(seed: 1337);
        var worldWarmTank = TestHelpers.CreateSeededWorld(seed: 1337);
        var coolTank = worldCoolTank.TankList.Tanks[0];
        var warmTank = worldWarmTank.TankList.Tanks[0];
        var zonePos = new Position(worldCoolTank.Terrain.Width / 2, worldCoolTank.Terrain.Height / 2);

        SetupHotZoneScenario(worldCoolTank, coolTank, zonePos, initialHeat: 10f);
        SetupHotZoneScenario(worldWarmTank, warmTank, zonePos, initialHeat: 90f);

        float coolStart = coolTank.Heat;
        float warmStart = warmTank.Heat;

        for (int i = 0; i < 30; i++)
        {
            coolTank.Advance(worldCoolTank, default);
            warmTank.Advance(worldWarmTank, default);
        }

        float coolGain = coolTank.Heat - coolStart;
        float warmGain = warmTank.Heat - warmStart;

        Assert.True(coolGain > 0f, $"Expected cool tank to heat up in hot terrain, got {coolGain:0.000}");
        Assert.True(coolGain > warmGain,
            $"Expected larger heat gain for lower starting heat. coolGain={coolGain:0.000}, warmGain={warmGain:0.000}");
    }

    [Fact]
    public void TankHeat_AboveSafeMax_DamagesTank()
    {
        var world = TestHelpers.CreateSeededWorld(seed: 2026);
        var tank = world.TankList.Tanks[0];
        var zonePos = new Position(world.Terrain.Width / 2, world.Terrain.Height / 2);

        SetupHotZoneScenario(world, tank, zonePos, initialHeat: 110f);
        int healthBefore = tank.Reactor.Health;

        tank.Advance(world, default);

        Assert.True(tank.Heat > 100f, $"Expected heat to remain above 100, got {tank.Heat:0.00}");
        Assert.True(tank.Reactor.Health < healthBefore,
            $"Expected overheat damage above 100 heat. before={healthBefore}, after={tank.Reactor.Health}");
    }

    [Fact]
    public void TankHeat_ExplosionZone_RaisesHeatWellAboveTwentyFive()
    {
        var world = TestHelpers.CreateSeededWorld(seed: 8080);
        var tank = world.TankList.Tanks[0];
        var zonePos = new Position(world.Terrain.Width / 2, world.Terrain.Height / 2);

        tank.Position = zonePos;
        ClearArea(world.Terrain, zonePos, radius: 8);
        tank.Heat = 20f;
        tank.Reactor.Current.Heat = new Heat(20);

        // Simulate repeated explosion deposition while standing in place.
        for (int i = 0; i < 8; i++)
            world.Terrain.AddHeatRadius(zonePos, Tweaks.Explosion.BulletHeatAmount, Tweaks.Explosion.BulletHeatRadius);

        for (int i = 0; i < 45; i++)
            tank.Advance(world, default);

        Assert.True(tank.Heat > 20.5f, $"Expected explosion heat zone to push above ambient after impact heating reduction. Actual={tank.Heat:0.00}");
    }

    [Fact]
    public void TankHeat_AtOwnBase_DoesNotHeatFillBaseInterior()
    {
        var world = TestHelpers.CreateSeededWorld(seed: 9090);
        var tank = world.TankList.Tanks[0];
        Assert.NotNull(tank.Base);
        var basePos = tank.Base!.Position;

        tank.Position = basePos;
        tank.Heat = 140f;
        tank.Reactor.Current.Heat = new Heat(140);

        // Start from cold terrain in/around base to isolate tank->terrain effects.
        CoolAreaToZero(world.Terrain, basePos, radius: 24);

        for (int i = 0; i < 120; i++)
            tank.Advance(world, default);

        float baseInteriorHeat = world.Terrain.SampleAverageHeat(basePos, radius: 14) * 255f;

        Assert.True(tank.Heat < 8f, $"Expected own base cooling to drop tank heat near 0. Actual={tank.Heat:0.00}");
        Assert.True(baseInteriorHeat < 8f,
            $"Expected base interior to stay cool, not heat-fill. Avg terrain heat={baseInteriorHeat:0.00}");
    }

    [Fact]
    public void WorldAdvance_BaseInterior_RemainsCoolUnderThermalExchange()
    {
        var world = TestHelpers.CreateSeededWorld(seed: 9191);
        var tank = world.TankList.Tanks[0];
        Assert.NotNull(tank.Base);
        var basePos = tank.Base!.Position;

        tank.Position = basePos;
        tank.Heat = 150f;
        tank.Reactor.Current.Heat = new Heat(150);

        // Deliberately heat the area around base, then let world simulation run.
        world.Terrain.AddHeatRadius(basePos, 255, 18);
        for (int i = 0; i < 200; i++)
            world.Advance(_ => default);

        float baseInteriorHeat = world.Terrain.SampleAverageHeat(basePos, radius: 12) * 255f;
        Assert.True(baseInteriorHeat < 8f,
            $"Expected base interior to remain near 0 with high base conductance. Avg terrain heat={baseInteriorHeat:0.00}");
    }

    private static void SetupHotZoneScenario(World world, Tank tank, Position zonePos, float initialHeat)
    {
        tank.Position = zonePos;
        ClearArea(world.Terrain, zonePos, radius: 6);
        world.Terrain.AddHeatRadius(zonePos, amount: 255, radius: 6);
        tank.Heat = initialHeat;
        tank.Reactor.Current.Heat = new Heat((int)MathF.Round(initialHeat));
    }

    private static void ClearArea(TerrainGrid terrain, Position center, int radius)
    {
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                var pos = new Position(x, y);
                if (!terrain.IsInside(pos))
                    continue;
                terrain.SetPixel(pos, TerrainPixel.Blank);
            }
    }

    private static void CoolAreaToZero(TerrainGrid terrain, Position center, int radius)
    {
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                var pos = new Position(x, y);
                if (!terrain.IsInside(pos))
                    continue;
                terrain.AddHeat(pos, -255);
            }
    }
}
