using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Input;
using Tunnerer.Core.Resources;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

public class TankHeatBehaviorTests
{
    private static readonly object StoneAmbientToggleGate = new();
    private static readonly ControllerOutput IdleInput = default;

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

        // Shot heat is applied once via TankHeatModel, then same-frame base ambient exchange cools it.
        Assert.InRange(tank.Heat, 0.15f, 0.25f);
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
        SetTankHeat(tank, 45f); // simulate a one-shot starting spike
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
        Assert.InRange(tank.Heat, 0f, 6f);
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
            // Keep injecting local explosion heat while tank remains in the zone.
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

        // Start from cold terrain in/around base to isolate tank->terrain effects.
        CoolAreaToZero(world.Terrain, basePos, radius: 24);
        visual.Capture(world, "start");

        AdvanceTank(tank, world, frames: 120, visual, phase: "advance", captureEvery: 12);
        visual.Capture(world, "end");

        float baseInteriorHeat = world.Terrain.SampleAverageHeat(basePos, radius: 14) * 255f;

        Assert.True(tank.Heat < 8f, $"Expected own base cooling to drop tank heat near 0. Actual={tank.Heat:0.00}");
        Assert.True(baseInteriorHeat < 8f,
            $"Expected base interior to stay cool, not heat-fill. Avg terrain heat={baseInteriorHeat:0.00}");
    }

    [Fact]
    public void WorldAdvance_BaseInterior_RemainsCoolUnderThermalExchange()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9191);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_BaseInterior_RemainsCoolUnderThermalExchange));
        var basePos = RequireBasePosition(tank);

        tank.Position = basePos;
        SetTankHeat(tank, 150f);

        // Deliberately heat the area around base, then let world simulation run.
        world.Terrain.AddHeatRadius(basePos, 255, 18);
        visual.Capture(world, "after_heat_injection");
        AdvanceWorld(world, frames: 200, visual: visual, phase: "settle", captureEvery: 20);
        visual.Capture(world, "end");

        float baseInteriorHeat = world.Terrain.SampleAverageHeat(basePos, radius: 12) * 255f;
        Assert.True(baseInteriorHeat < 8f,
            $"Expected base interior to remain near 0 with high base conductance. Avg terrain heat={baseInteriorHeat:0.00}");
    }

    [Fact]
    public void WorldAdvance_HeatConnectedToBase_DecaysOverTime()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9393);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_HeatConnectedToBase_DecaysOverTime));
        var basePos = RequireBasePosition(tank);

        // Build a clear conductive corridor from the base entrance to a hot patch.
        CarveVerticalLine(world.Terrain, basePos, startDy: 0, endDyInclusive: 39);

        var hotPos = basePos + new Offset(0, 36);
        world.Terrain.AddHeatRadius(hotPos, 255, 10);
        visual.Capture(world, "after_heat_injection");
        float startHeat = world.Terrain.SampleAverageHeat(hotPos, radius: 8) * 255f;

        AdvanceWorld(world, frames: 420, visual: visual, phase: "settle", captureEvery: 30);
        visual.Capture(world, "end");

        float endHeat = world.Terrain.SampleAverageHeat(hotPos, radius: 8) * 255f;
        Assert.True(endHeat < startHeat * 0.85f,
            $"Expected connected system to cool over time via base sink. start={startHeat:0.00}, end={endHeat:0.00}");
    }

    [Fact]
    public void WorldAdvance_SingleBulletHeat_ConnectedToBase_CoolsEventually()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9494);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_SingleBulletHeat_ConnectedToBase_CoolsEventually));
        var basePos = RequireBasePosition(tank);

        // Carve a conductive corridor from base into cave area.
        CarveVerticalLine(world.Terrain, basePos, startDy: 0, endDyInclusive: 49);

        var impactPos = basePos + new Offset(0, 46);
        world.Terrain.AddHeatRadius(impactPos, Tweaks.Explosion.BulletHeatAmount, Tweaks.Explosion.BulletHeatRadius);
        visual.Capture(world, "after_heat_injection");
        float startHeat = world.Terrain.GetHeatTemperature(impactPos);

        AdvanceUntil(world, maxFrames: 24 * 60, () =>
            world.Terrain.GetHeatTemperature(impactPos) <= startHeat - 1f,
            visual,
            "settle");

        float endHeat = world.Terrain.GetHeatTemperature(impactPos);
        visual.Capture(world, "end");
        Assert.True(endHeat < startHeat,
            $"Expected one-bullet heat packet to cool over time. start={startHeat}, end={endHeat}");
        Assert.True(endHeat <= startHeat - 1f,
            $"Expected measurable dissipation when connected to base sink. start={startHeat}, end={endHeat}");
    }

    [Fact]
    public void WorldAdvance_BaseConnectedTunnel_ExternalHeat_ConvergesToBaseTemperature()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9898);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_BaseConnectedTunnel_ExternalHeat_ConvergesToBaseTemperature));
        var basePos = RequireBasePosition(tank);

        // Build a narrow tunnel from the base to an external hotspot.
        CarveVerticalLine(world.Terrain, basePos, startDy: 0, endDyInclusive: 72);

        // Inject at the tunnel boundary (stone next to air), so both materials get heated.
        var hotspotPos = basePos + new Offset(1, 66);
        world.Terrain.AddHeatRadius(hotspotPos, amount: 255, radius: 12);
        visual.Capture(world, "after_heat_injection");

        float startHotspot = world.Terrain.SampleAverageHeat(hotspotPos, radius: 8) * 255f;
        float startBaseTemperature = world.Terrain.SampleAverageHeat(basePos, radius: 6) * 255f;

        // Let the connected system settle; base should act like a strong sink.
        AdvanceUntil(world, maxFrames: 24 * 90, () =>
            world.Terrain.SampleAverageHeat(hotspotPos, radius: 8) * 255f <= 6f,
            visual,
            "settle");

        float endHotspot = world.Terrain.SampleAverageHeat(hotspotPos, radius: 8) * 255f;
        float endBaseTemperature = world.Terrain.SampleAverageHeat(basePos, radius: 6) * 255f;
        visual.Capture(world, "end");

        Assert.True(endHotspot < startHotspot * 0.25f,
            $"Expected strong decay through base-connected tunnel. startHotspot={startHotspot:0.00}, endHotspot={endHotspot:0.00}");
        Assert.True(endHotspot <= endBaseTemperature + 6f,
            $"Expected hotspot to converge near base temperature. endHotspot={endHotspot:0.00}, endBaseTemperature={endBaseTemperature:0.00}, startBaseTemperature={startBaseTemperature:0.00}");
    }

    [Fact]
    public void WorldAdvance_NoExternalHeating_SystemEnergyDoesNotIncreasePerTick()
    {
        var world = TestHelpers.CreateSeededWorld(seed: 9595);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_NoExternalHeating_SystemEnergyDoesNotIncreasePerTick));

        // Place each tank in its own base and pre-heat tank + nearby terrain.
        // In this setup, ambient cannot inject heat into tanks (base ambient = 0),
        // so total system energy should be monotone non-increasing.
        foreach (var tank in world.TankList.Tanks)
        {
            var basePos = RequireBasePosition(tank);
            tank.Position = basePos;
            SetTankHeat(tank, 140f);
            world.Terrain.AddHeatRadius(basePos, amount: 220, radius: 10);
        }

        double previousEnergy = SumSystemThermalEnergy(world);
        double startEnergy = previousEnergy;
        double maxUpstep = 0.0;
        double cumulativeUpsteps = 0.0;
        visual.Capture(world, "start");

        for (int i = 0; i < 420; i++)
        {
            world.Advance(_ => default);
            double currentEnergy = SumSystemThermalEnergy(world);
            double upstep = currentEnergy - previousEnergy;
            if (upstep > 0.0)
            {
                cumulativeUpsteps += upstep;
                if (upstep > maxUpstep)
                    maxUpstep = upstep;
            }

            previousEnergy = currentEnergy;
            if ((i % 30) == 0)
                visual.Capture(world, $"advance_{i:D3}");
        }
        visual.Capture(world, "end");

        Assert.True(maxUpstep <= 0.75,
            $"Expected no positive energy spikes per tick. maxUpstep={maxUpstep:0.000}");
        Assert.True(cumulativeUpsteps <= 5.0,
            $"Expected negligible cumulative positive drift. cumulativeUpsteps={cumulativeUpsteps:0.000}");
        Assert.True(previousEnergy < startEnergy,
            $"Expected net cooling over the scenario. start={startEnergy:0.000}, end={previousEnergy:0.000}");
    }

    [Fact]
    public void WorldAdvance_GameLikeInputs_NoTankSettlesNearOneTwenty()
    {
        // Mirror game-like cadence/settings: default map, deterministic seed, 24 Hz advance.
        var world = TestHelpers.CreateSeededWorld(seed: TestHelpers.DefaultSeed);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_GameLikeInputs_NoTankSettlesNearOneTwenty));
        var tanks = world.TankList.Tanks;
        var bot = new BotTankAI(seed: TestHelpers.DefaultSeed + 2);

        const float plateauCenter = 120f;
        const float plateauHalfBand = 10f;
        int totalFrames = Tweaks.Perf.TargetFps * 75; // shorten runtime, still long enough to expose plateaus.
        int tailWindow = Tweaks.Perf.TargetFps * 15;  // analyze final 15 seconds.

        var tailByTank = new Queue<float>[tanks.Count];
        for (int i = 0; i < tanks.Count; i++)
            tailByTank[i] = new Queue<float>(tailWindow);

        for (int frame = 0; frame < totalFrames; frame++)
        {
            world.Advance(i =>
            {
                if (i == 0)
                    return default; // player slot idle (no keyboard input)

                Tank enemy = tanks[0];
                return bot.GetInput(tanks[i], enemy, world.Terrain);
            });

            for (int i = 0; i < tanks.Count; i++)
            {
                var q = tailByTank[i];
                if (q.Count == tailWindow)
                    q.Dequeue();
                q.Enqueue(tanks[i].Heat);
            }

            if ((frame % Tweaks.Perf.TargetFps) == 0)
                visual.Capture(world, $"advance_{frame:D4}");
        }
        visual.Capture(world, "end");

        // Idle local-player slot should stay cool under game-like steps.
        float playerTailAverage = tailByTank[0].Average();
        Assert.True(playerTailAverage < 45f,
            $"Expected idle player tank to remain cool. tailAvg={playerTailAverage:0.00}");

        for (int i = 0; i < tanks.Count; i++)
        {
            float mean = tailByTank[i].Average();
            float stdDev = StdDev(tailByTank[i], mean);
            float min = tailByTank[i].Min();
            float max = tailByTank[i].Max();
            bool looksLike120Plateau = mean >= (plateauCenter - plateauHalfBand) &&
                                       mean <= (plateauCenter + plateauHalfBand) &&
                                       stdDev < 8f;

            Assert.False(looksLike120Plateau,
                $"Tank {i} appears to settle near 120. mean={mean:0.00}, stdDev={stdDev:0.00}, min={min:0.00}, max={max:0.00}");
        }
    }

    [Fact]
    public void WorldAdvance_SingleBulletIntoOutsideWall_HotspotEventuallyCoolsOut()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9899);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_SingleBulletIntoOutsideWall_HotspotEventuallyCoolsOut));
        var basePos = RequireBasePosition(tank);

        tank.Position = basePos;
        SetTankHeat(tank, 0f);
        visual.Capture(world, "start");

        // Shoot outward from base into a known rock wall location outside the base.
        var impactPos = basePos + new Offset(0, 48);
        CarveVerticalLine(world.Terrain, basePos, startDy: 0, endDyInclusive: 46);
        world.Terrain.SetPixel(impactPos, TerrainPixel.Rock);
        world.Terrain.SetPixel(impactPos + new Offset(1, 0), TerrainPixel.Rock);
        world.Terrain.SetPixel(impactPos + new Offset(-1, 0), TerrainPixel.Rock);

        float hotspotAtStart = world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f;

        // Inject exactly one gameplay bullet toward the outside wall.
        var spawnPos = impactPos + new Offset(0, -12);
        world.Projectiles.Add(Tunnerer.Core.Entities.Projectiles.Projectile.CreateBullet(
            spawnPos,
            new VectorF(0f, Tweaks.Weapon.CannonBulletSpeed),
            tank.Color));

        // Allow projectile collision and spawned explosion/shrapnel to resolve.
        AdvanceWorld(world, frames: 20, visual: visual, phase: "resolve_hit", captureEvery: 4);

        float hotspotAfterHit = world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f;
        Assert.True(hotspotAfterHit > hotspotAtStart + 1f,
            $"Expected bullet impact to increase local heat. start={hotspotAtStart:0.00}, afterHit={hotspotAfterHit:0.00}");

        AdvanceUntil(world, maxFrames: 24 * 90, () =>
            world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f <= 6f,
            visual,
            "settle");

        float hotspotAfterSettle = world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f;
        visual.Capture(world, "end");
        Assert.True(hotspotAfterSettle <= 6f,
            $"Expected one-shot outside-wall hotspot to cool near zero eventually. afterHit={hotspotAfterHit:0.00}, afterSettle={hotspotAfterSettle:0.00}");
    }

    [Fact]
    public void WorldAdvance_ShootWaitRevisit_HotspotShouldDissipateBeforeReturn()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9797);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_ShootWaitRevisit_HotspotShouldDissipateBeforeReturn));
        var basePos = RequireBasePosition(tank);

        tank.Position = basePos;
        SetTankHeat(tank, 0f);
        visual.Capture(world, "start");

        // Recreate the gameplay scenario: fire outward from base, wait, then drive back there.
        var impactPos = basePos + new Offset(0, 46);
        CarveVerticalLine(world.Terrain, basePos, startDy: 0, endDyInclusive: 48);

        // Ensure bullets collide around a known hotspot instead of flying indefinitely.
        world.Terrain.SetPixel(impactPos, TerrainPixel.Rock);
        world.Terrain.SetPixel(impactPos + new Offset(1, 0), TerrainPixel.Rock);
        world.Terrain.SetPixel(impactPos + new Offset(-1, 0), TerrainPixel.Rock);
        float hotspotAtStart = world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f;

        var shootDown = new ControllerOutput
        {
            AimDirection = new DirectionF(0f, 1f),
            ShootPrimary = true,
        };
        var moveDown = new ControllerOutput
        {
            MoveSpeed = new Offset(0, 1),
            AimDirection = new DirectionF(0f, 1f),
        };

        const int shootFrames = 72; // ~24 shots at turret cadence.
        AdvanceWorld(world, frames: shootFrames, frameInputForTank0: _ => shootDown, visual: visual, phase: "shoot", captureEvery: 6);
        float hotspotAfterShooting = world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f;

        const int waitFrames = 24 * 12; // 12 seconds "wait a bit".
        AdvanceWorld(world, frames: waitFrames, visual: visual, phase: "wait", captureEvery: 24);

        float hotspotBeforeReturn = world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f;

        float peakHeatOnReturn = tank.Heat;
        int travelGuard = 0;
        while (Position.DistanceSquared(tank.Position, impactPos) > 3 * 3 && travelGuard++ < 600)
        {
            world.Advance(idx => idx == 0 ? moveDown : default);
            if ((travelGuard % 20) == 0)
                visual.Capture(world, $"return_{travelGuard:D3}");
            if (tank.Heat > peakHeatOnReturn)
                peakHeatOnReturn = tank.Heat;
        }

        const int dwellFrames = 24 * 3;
        for (int i = 0; i < dwellFrames; i++)
        {
            world.Advance(_ => default);
            if ((i % 12) == 0)
                visual.Capture(world, $"dwell_{i:D3}");
            if (tank.Heat > peakHeatOnReturn)
                peakHeatOnReturn = tank.Heat;
        }

        bool hotspotOk = hotspotBeforeReturn < 70f;
        bool peakOk = peakHeatOnReturn < 90f;
        visual.Capture(world, "end");
        Assert.True(hotspotOk && peakOk,
            $"Expected hotspot dissipation and safe revisit. start={hotspotAtStart:0.00}, afterShoot={hotspotAfterShooting:0.00}, beforeReturn={hotspotBeforeReturn:0.00}, peakOnReturn={peakHeatOnReturn:0.00}");
    }

    [Fact]
    public void WorldAdvance_DriveThroughTunnel_HeatTrailAndBaseEventuallyCool()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9901);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_DriveThroughTunnel_HeatTrailAndBaseEventuallyCool));
        var basePos = RequireBasePosition(tank);

        tank.Position = basePos;
        SetTankHeat(tank, 0f);
        visual.Capture(world, "start");

        // Carve a single-lane tunnel outside the base door.
        int firstOutsideDy = (Tweaks.Base.BaseSize / 2) + 1;
        CarveVerticalLine(world.Terrain, basePos, firstOutsideDy, firstOutsideDy + 56);

        var driveDown = MoveAndAim(0, 1, 0f, 1f);
        var driveUp = MoveAndAim(0, -1, 0f, -1f);

        // Drive out and back without shooting.
        for (int i = 0; i < 60; i++)
        {
            world.Advance(idx => idx == 0 ? driveDown : default);
            visual.Capture(world, $"drive_out_{i:D3}");
        }
        for (int i = 0; i < 60; i++)
        {
            world.Advance(idx => idx == 0 ? driveUp : default);
            visual.Capture(world, $"drive_back_{i:D3}");
        }

        // Park back in base so the settle phase measures trail cooling, not continued deposition.
        tank.Position = basePos;

        float baseAfterDrive = world.Terrain.SampleAverageHeat(basePos, radius: 10) * 255f;
        Assert.True(tank.Heat > 0f,
            $"Expected driving to accumulate some tank heat. tankHeat={tank.Heat:0.00}");

        AdvanceUntil(world, maxFrames: 24 * 120, () =>
            world.Terrain.SampleAverageHeat(basePos, radius: 10) * 255f <= 6f,
            visual,
            "settle_after_drive");

        float baseAfterSettle = world.Terrain.SampleAverageHeat(basePos, radius: 10) * 255f;
        Assert.True(baseAfterSettle <= 6f,
            $"Expected base interior to remain/cool near ambient. baseAfterDrive={baseAfterDrive:0.00}, baseAfterSettle={baseAfterSettle:0.00}");
    }

    [Fact]
    public void WorldAdvance_DriveOutOfBase_BaseInteriorStaysCool()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9902, enableTerrainRegrowth: false);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_DriveOutOfBase_BaseInteriorStaysCool));
        var basePos = RequireBasePosition(tank);

        tank.Position = basePos;
        SetTankHeat(tank, 0f);
        CoolAreaToZero(world.Terrain, basePos, radius: 14);
        visual.Capture(world, "start");

        // Carve an exit tunnel outside the base door.
        int firstOutsideDy = (Tweaks.Base.BaseSize / 2) + 1;
        CarveVerticalLine(world.Terrain, basePos, firstOutsideDy, firstOutsideDy + 56);

        var driveDown = MoveAndAim(0, 1, 0f, 1f);

        for (int i = 0; i < 60; i++)
        {
            world.Advance(idx => idx == 0 ? driveDown : default);
            visual.Capture(world, $"drive_out_{i:D3}");
        }

        float baseAfterDriveOut = world.Terrain.SampleAverageHeat(basePos, radius: 10) * 255f;

        AdvanceUntil(world, maxFrames: 24 * 60, () =>
            world.Terrain.SampleAverageHeat(basePos, radius: 10) * 255f <= 6f,
            visual,
            "settle_after_drive_out");

        float baseAfterSettle = world.Terrain.SampleAverageHeat(basePos, radius: 10) * 255f;
        Assert.True(baseAfterSettle <= 6f,
            $"Expected base interior to remain near ambient after driving out. baseAfterDriveOut={baseAfterDriveOut:0.00}, baseAfterSettle={baseAfterSettle:0.00}");
    }

    [Fact]
    public void WorldAdvance_ExteriorTunnelHotspot_CoolsViaBaseBoundaryExchange_NormalRules()
    {
        RunExteriorTunnelHotspotScenario(disableStoneAmbientExchange: false);
    }

    [Fact]
    public void WorldAdvance_ExteriorTunnelHotspot_CoolsViaBaseBoundaryExchange_StoneAmbientOff()
    {
        RunExteriorTunnelHotspotScenario(disableStoneAmbientExchange: true);
    }

    private static void RunExteriorTunnelHotspotScenario(bool disableStoneAmbientExchange)
    {
        lock (StoneAmbientToggleGate)
        {
            bool originalStoneAmbient = Tweaks.World.EnableStoneAmbientExchange;
            Tweaks.World.EnableStoneAmbientExchange = !disableStoneAmbientExchange;
            try
            {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9903, enableTerrainRegrowth: false);
        using var visual = TestVisualTrace.Start(disableStoneAmbientExchange
            ? nameof(WorldAdvance_ExteriorTunnelHotspot_CoolsViaBaseBoundaryExchange_StoneAmbientOff)
            : nameof(WorldAdvance_ExteriorTunnelHotspot_CoolsViaBaseBoundaryExchange_NormalRules));
        var basePos = RequireBasePosition(tank);
        int half = Tweaks.Base.BaseSize / 2;

        // Keep base walls/door intact; only carve outside the door.
        int firstOutsideDy = half + 1;
        CarveVerticalLine(world.Terrain, basePos, firstOutsideDy, firstOutsideDy + 56);

        var doorPos = basePos + new Offset(0, half);
        Assert.Equal(ThermalMaterial.Base, Pixel.GetThermalMaterial(world.Terrain.GetPixelRaw(doorPos)));

        var hotspotPos = basePos + new Offset(1, firstOutsideDy + 34);
        world.Terrain.AddHeatRadius(hotspotPos, amount: 255, radius: 10);
        visual.Capture(world, "after_heat_injection");

        double SumTunnelEnergy()
        {
            double sum = 0.0;
            int y0 = basePos.Y + firstOutsideDy;
            int y1 = basePos.Y + firstOutsideDy + 56;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = basePos.X - 1; x <= basePos.X + 1; x++)
                {
                    var p = new Position(x, y);
                    if (!world.Terrain.IsInside(p))
                        continue;
                    float c = ThermalCapacityFor(world.Terrain.GetPixelRaw(p));
                    sum += world.Terrain.GetHeatTemperature(p) * c;
                }
            }

            return sum;
        }

        float startHotspot = world.Terrain.SampleAverageHeat(hotspotPos, radius: 6) * 255f;
        var energySamples = new List<string>();
        double startTunnelEnergy = SumTunnelEnergy();
        const int maxFrames = 24 * 120;
        for (int i = 0; i < maxFrames; i++)
        {
            if ((i % 120) == 0)
                energySamples.Add($"{i}:{SumTunnelEnergy():0.0}");

            if (world.Terrain.SampleAverageHeat(hotspotPos, radius: 6) * 255f <= 8f)
                break;

            world.Advance(_ => default);
            visual.Capture(world, $"settle_exterior_{i:D4}");
        }

        float endHotspot = world.Terrain.SampleAverageHeat(hotspotPos, radius: 6) * 255f;
        double endTunnelEnergy = SumTunnelEnergy();
        Assert.True(endHotspot < startHotspot * 0.35f,
            $"Expected strong decay through base boundary exchange. startHotspot={startHotspot:0.00}, endHotspot={endHotspot:0.00}");
        Assert.True(endHotspot <= 8f,
            $"Expected exterior tunnel hotspot to cool near ambient eventually. stoneAmbientOff={disableStoneAmbientExchange}, endHotspot={endHotspot:0.00}, tunnelEnergyStart={startTunnelEnergy:0.0}, tunnelEnergyEnd={endTunnelEnergy:0.0}, tunnelEnergySamples=[{string.Join(", ", energySamples)}]");
            }
            finally
            {
                Tweaks.World.EnableStoneAmbientExchange = originalStoneAmbient;
            }
        }
    }

    private static void SetupHotZoneScenario(World world, Tank tank, Position zonePos, float initialHeat)
    {
        tank.Position = zonePos;
        ClearArea(world.Terrain, zonePos, radius: 6);
        world.Terrain.AddHeatRadius(zonePos, amount: 255, radius: 6);
        SetTankHeat(tank, initialHeat);
    }

    private static (World world, Tank tank) CreateWorldWithPrimaryTank()
    {
        var world = TestHelpers.CreateSeededWorld();
        return (world, world.TankList.Tanks[0]);
    }

    private static (World world, Tank tank) CreateWorldWithPrimaryTank(int seed, bool enableTerrainRegrowth = true)
    {
        var world = TestHelpers.CreateSeededWorld(seed: seed, enableTerrainRegrowth: enableTerrainRegrowth);
        return (world, world.TankList.Tanks[0]);
    }

    private static Position GetWorldCenter(World world) =>
        new(world.Terrain.Width / 2, world.Terrain.Height / 2);

    private static Position RequireBasePosition(Tank tank)
    {
        Assert.NotNull(tank.Base);
        return tank.Base!.Position;
    }

    private static void SetTankHeat(Tank tank, float heat)
    {
        tank.Heat = heat;
        tank.Reactor.Current.Heat = new Heat((int)MathF.Round(heat));
    }

    private static ControllerOutput MoveAndAim(int moveX, int moveY, float aimX, float aimY) =>
        new()
        {
            MoveSpeed = new Offset(moveX, moveY),
            AimDirection = new DirectionF(aimX, aimY),
        };

    private static void CarveVerticalLine(TerrainGrid terrain, Position anchor, int startDy, int endDyInclusive)
    {
        for (int i = startDy; i <= endDyInclusive; i++)
        {
            var pos = anchor + new Offset(0, i);
            if (terrain.IsInside(pos))
                terrain.SetPixel(pos, TerrainPixel.Blank);
        }
    }

    private static void AdvanceTank(
        Tank tank,
        World world,
        int frames,
        TestVisualTrace? visual = null,
        string phase = "advance",
        int captureEvery = 0,
        Action<int>? beforeStep = null,
        Action<int>? afterStep = null)
    {
        for (int i = 0; i < frames; i++)
        {
            beforeStep?.Invoke(i);
            tank.Advance(world, IdleInput);
            if (captureEvery > 0 && (i % captureEvery) == 0)
                visual?.Capture(world, $"{phase}_{i:D3}");
            afterStep?.Invoke(i);
        }
    }

    private static void AdvanceWorld(
        World world,
        int frames,
        TestVisualTrace? visual = null,
        string phase = "advance",
        int captureEvery = 0)
    {
        for (int i = 0; i < frames; i++)
        {
            world.Advance(_ => IdleInput);
            if (captureEvery > 0 && (i % captureEvery) == 0)
                visual?.Capture(world, $"{phase}_{i:D3}");
        }
    }

    private static void AdvanceWorld(
        World world,
        int frames,
        Func<int, ControllerOutput> frameInputForTank0,
        TestVisualTrace? visual = null,
        string phase = "advance",
        int captureEvery = 0)
    {
        for (int i = 0; i < frames; i++)
        {
            var input = frameInputForTank0(i);
            world.Advance(idx => idx == 0 ? input : IdleInput);
            if (captureEvery > 0 && (i % captureEvery) == 0)
                visual?.Capture(world, $"{phase}_{i:D3}");
        }
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

    private static double SumSystemThermalEnergy(World world)
    {
        double total = 0.0;
        total += SumTerrainThermalEnergy(world.Terrain);
        foreach (var tank in world.TankList.Tanks)
            total += tank.Heat * Tweaks.Tank.TankHeatCapacity;
        return total;
    }

    private static void AdvanceUntil(World world, int maxFrames, Func<bool> done, TestVisualTrace? visual = null, string phase = "advance")
    {
        for (int i = 0; i < maxFrames; i++)
        {
            if (done())
                return;
            world.Advance(_ => default);
            visual?.Capture(world, $"{phase}_{i:D4}");
        }
    }

    private static double SumTerrainThermalEnergy(TerrainGrid terrain)
    {
        double total = 0.0;
        for (int i = 0; i < terrain.Size.Area; i++)
        {
            float capacity = ThermalCapacityFor(terrain.GetPixelRaw(i));
            total += terrain.GetHeatTemperature(i) * capacity;
        }

        return total;
    }

    private static float ThermalCapacityFor(TerrainPixel pixel) => Pixel.GetThermalMaterial(pixel) switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalCapacityAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalCapacityDirt,
        _ => Tweaks.World.ThermalCapacityStone,
    };

    private static float StdDev(IEnumerable<float> values, float mean)
    {
        double sumSq = 0.0;
        int n = 0;
        foreach (float v in values)
        {
            double d = v - mean;
            sumSq += d * d;
            n++;
        }

        return n > 0 ? (float)Math.Sqrt(sumSq / n) : 0f;
    }
}
