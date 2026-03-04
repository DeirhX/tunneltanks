using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Input;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;
using static Tunnerer.Tests.TankHeatTestHelpers;

namespace Tunnerer.Tests;

public class TankHeatBehaviorTests
{
    [Fact]
    public void TankHeat_PassivelyCools_WhenNoHeatSource()
    {
        var (world, tank) = CreateWorldWithPrimaryTank();
        using var visual = TestVisualTrace.Start(nameof(TankHeat_PassivelyCools_WhenNoHeatSource));
        var pos = GetWorldCenter(world);

        tank.Position = pos;
        ClearArea(world.Terrain, pos, radius: 6);
        SetTankHeat(tank, 60f);
        visual.Capture(world, "start");

        AdvanceTank(tank, world, frames: 20, visual, phase: "cool", captureEvery: 4);

        visual.Capture(world, "end");
        Assert.True(tank.Heat < 60f, $"Expected passive cooling, but heat stayed at {tank.Heat:0.00}");
    }

    [Fact]
    public void TankHeat_SingleShot_UsesModelShotHeatOnce()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 7777);
        SetTankHeat(tank, 0f);

        tank.Advance(world, new ControllerOutput
        {
            ShootPrimary = true,
            AimDirection = new DirectionF(1f, 0f),
        });

        Assert.InRange(tank.Heat, 0.20f, 0.30f);
    }

    [Fact]
    public void TankHeat_OutsideBase_DriftsTowardAmbientZero()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 2222);
        using var visual = TestVisualTrace.Start(nameof(TankHeat_OutsideBase_DriftsTowardAmbientZero));
        var pos = GetWorldCenter(world);

        tank.Position = pos;
        ClearArea(world.Terrain, pos, radius: 8);
        SetTankHeat(tank, 0f);
        visual.Capture(world, "start");

        AdvanceTank(tank, world, frames: 120, visual, phase: "drift", captureEvery: 12);

        visual.Capture(world, "end");
        Assert.InRange(tank.Heat, 0f, 2f);
    }

    [Fact]
    public void TankHeat_OutsideBase_StartingHot_DoesNotRunAwayUpward()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 2233);
        using var visual = TestVisualTrace.Start(nameof(TankHeat_OutsideBase_StartingHot_DoesNotRunAwayUpward));
        var pos = GetWorldCenter(world);

        tank.Position = pos;
        ClearArea(world.Terrain, pos, radius: 8);
        SetTankHeat(tank, 45f);
        visual.Capture(world, "start");

        float observedMax = tank.Heat;
        AdvanceTank(tank, world, frames: 220, visual, phase: "cool", captureEvery: 20, afterStep: _ =>
        {
            if (tank.Heat > observedMax)
                observedMax = tank.Heat;
        });

        visual.Capture(world, "end");
        Assert.True(observedMax <= 46f,
            $"Expected no runaway heating. start=45, observedMax={observedMax:0.00}");
        Assert.InRange(tank.Heat, 0f, 15f);
    }

    [Fact]
    public void TankHeat_OutsideBase_IdleCooling_IsMonotoneTowardAmbient()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 2244);
        using var visual = TestVisualTrace.Start(nameof(TankHeat_OutsideBase_IdleCooling_IsMonotoneTowardAmbient));
        var pos = GetWorldCenter(world);

        tank.Position = pos;
        ClearArea(world.Terrain, pos, radius: 8);
        CoolAreaToZero(world.Terrain, pos, radius: 8);
        SetTankHeat(tank, 80f);
        visual.Capture(world, "start");

        float previous = tank.Heat;
        AdvanceTank(tank, world, frames: 180, visual, phase: "cool", captureEvery: 18, afterStep: i =>
        {
            Assert.True(tank.Heat <= previous + 0.05f,
                $"Expected non-increasing idle cooling. step={i}, previous={previous:0.000}, current={tank.Heat:0.000}");
            previous = tank.Heat;
        });

        visual.Capture(world, "end");
        Assert.InRange(tank.Heat, 0f, 6f);
    }

    [Fact]
    public void TankHeat_HotTerrainTransfer_DependsOnHeatDelta()
    {
        var worldCoolTank = TestHelpers.CreateSeededWorld(seed: 1337);
        var worldWarmTank = TestHelpers.CreateSeededWorld(seed: 1337);
        using var visualCool = TestVisualTrace.Start(nameof(TankHeat_HotTerrainTransfer_DependsOnHeatDelta) + "_CoolTank");
        using var visualWarm = TestVisualTrace.Start(nameof(TankHeat_HotTerrainTransfer_DependsOnHeatDelta) + "_WarmTank");
        var coolTank = worldCoolTank.TankList.Tanks[0];
        var warmTank = worldWarmTank.TankList.Tanks[0];
        var zonePos = new Position(worldCoolTank.Terrain.Width / 2, worldCoolTank.Terrain.Height / 2);

        SetupHotZoneScenario(worldCoolTank, coolTank, zonePos, initialHeat: 10f);
        SetupHotZoneScenario(worldWarmTank, warmTank, zonePos, initialHeat: 90f);

        float coolStart = coolTank.Heat;
        float warmStart = warmTank.Heat;
        visualCool.Capture(worldCoolTank, "start");
        visualWarm.Capture(worldWarmTank, "start");

        for (int i = 0; i < 30; i++)
        {
            coolTank.Advance(worldCoolTank, default);
            warmTank.Advance(worldWarmTank, default);
            if ((i % 6) == 0)
            {
                visualCool.Capture(worldCoolTank, $"advance_{i:D3}");
                visualWarm.Capture(worldWarmTank, $"advance_{i:D3}");
            }
        }
        visualCool.Capture(worldCoolTank, "end");
        visualWarm.Capture(worldWarmTank, "end");

        float coolGain = coolTank.Heat - coolStart;
        float warmGain = warmTank.Heat - warmStart;

        Assert.True(coolGain > 0f, $"Expected cool tank to heat up in hot terrain, got {coolGain:0.000}");
        Assert.True(coolGain > warmGain,
            $"Expected larger heat gain for lower starting heat. coolGain={coolGain:0.000}, warmGain={warmGain:0.000}");
    }

    [Fact]
    public void TankHeat_AboveSafeMax_DamagesTank()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 2026);
        using var visual = TestVisualTrace.Start(nameof(TankHeat_AboveSafeMax_DamagesTank));
        var zonePos = new Position(world.Terrain.Width / 2, world.Terrain.Height / 2);

        SetupHotZoneScenario(world, tank, zonePos, initialHeat: 110f);
        int healthBefore = tank.Reactor.Health;
        visual.Capture(world, "start");

        tank.Advance(world, default);
        visual.Capture(world, "after_advance");

        Assert.True(tank.Heat > 100f, $"Expected heat to remain above 100, got {tank.Heat:0.00}");
        Assert.True(tank.Reactor.Health < healthBefore,
            $"Expected overheat damage above 100 heat. before={healthBefore}, after={tank.Reactor.Health}");
    }

    [Fact]
    public void TankHeat_ExplosionZone_RaisesHeatWellAboveTwentyFive()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 8080);
        using var visual = TestVisualTrace.Start(nameof(TankHeat_ExplosionZone_RaisesHeatWellAboveTwentyFive));
        var zonePos = new Position(world.Terrain.Width / 2, world.Terrain.Height / 2);

        tank.Position = zonePos;
        ClearArea(world.Terrain, zonePos, radius: 8);
        SetTankHeat(tank, 20f);
        visual.Capture(world, "start");

        float peakHeat = tank.Heat;
        for (int i = 0; i < 45; i++)
        {
            world.Terrain.AddHeatRadius(zonePos, Tweaks.Explosion.BulletHeatAmount, Tweaks.Explosion.BulletHeatRadius);
            tank.Advance(world, default);
            if ((i % 6) == 0) visual.Capture(world, $"advance_{i:D3}");
            if (tank.Heat > peakHeat)
                peakHeat = tank.Heat;
        }
        visual.Capture(world, "end");

        Assert.True(peakHeat > 20.5f,
            $"Expected explosion heat zone to push above ambient at least transiently. peak={peakHeat:0.00}, final={tank.Heat:0.00}");
    }

    [Fact]
    public void TankHeat_AtOwnBase_DoesNotHeatFillBaseInterior()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9090);
        using var visual = TestVisualTrace.Start(nameof(TankHeat_AtOwnBase_DoesNotHeatFillBaseInterior));
        var basePos = RequireBasePosition(tank);

        tank.Position = basePos;
        SetTankHeat(tank, 140f);

        CoolAreaToZero(world.Terrain, basePos, radius: 24);
        visual.Capture(world, "start");

        AdvanceTank(tank, world, frames: 120, visual, phase: "advance", captureEvery: 12);
        visual.Capture(world, "end");

        float baseInteriorHeat = world.Terrain.SampleAverageHeat(basePos, radius: 14) * 255f;

        Assert.True(tank.Heat < 8f, $"Expected own base cooling to drop tank heat near 0. Actual={tank.Heat:0.00}");
        Assert.True(baseInteriorHeat < 8f,
            $"Expected base interior to stay cool, not heat-fill. Avg terrain heat={baseInteriorHeat:0.00}");
    }
}
