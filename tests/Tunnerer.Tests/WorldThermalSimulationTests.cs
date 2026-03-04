using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Input;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;
using static Tunnerer.Tests.TankHeatTestHelpers;

namespace Tunnerer.Tests;

public class WorldThermalSimulationTests
{
    [Fact]
    public void WorldAdvance_BaseInterior_RemainsCoolUnderThermalExchange()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9191);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_BaseInterior_RemainsCoolUnderThermalExchange));
        var basePos = RequireBasePosition(tank);

        tank.Position = basePos;
        SetTankHeat(tank, 150f);

        world.Terrain.AddHeatRadius(basePos, 255, 18);
        visual.Capture(world, "after_heat_injection");
        AdvanceWorld(world, frames: 200, visual: visual, phase: "settle", captureEvery: 20);
        visual.Capture(world, "end");

        float baseInteriorHeat = world.Terrain.SampleAverageHeat(basePos, radius: 12) * 255f;
        Assert.True(baseInteriorHeat < 20f,
            $"Expected base interior to stay bounded under conservative exchange. Avg terrain heat={baseInteriorHeat:0.00}");
    }

    [Fact]
    public void WorldAdvance_HeatConnectedToBase_DecaysOverTime()
    {
        var (world, tank) = CreateWorldWithPrimaryTank(seed: 9393);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_HeatConnectedToBase_DecaysOverTime));
        var basePos = RequireBasePosition(tank);

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

        CarveVerticalLine(world.Terrain, basePos, startDy: 0, endDyInclusive: 72);

        var hotspotPos = basePos + new Offset(1, 66);
        world.Terrain.AddHeatRadius(hotspotPos, amount: 255, radius: 12);
        visual.Capture(world, "after_heat_injection");

        float startHotspot = world.Terrain.SampleAverageHeat(hotspotPos, radius: 8) * 255f;
        float startBaseTemperature = world.Terrain.SampleAverageHeat(basePos, radius: 6) * 255f;

        AdvanceUntil(world, maxFrames: 24 * 90, () =>
            world.Terrain.SampleAverageHeat(hotspotPos, radius: 8) * 255f <= 6f,
            visual,
            "settle");

        float endHotspot = world.Terrain.SampleAverageHeat(hotspotPos, radius: 8) * 255f;
        float endBaseTemperature = world.Terrain.SampleAverageHeat(basePos, radius: 6) * 255f;
        visual.Capture(world, "end");

        Assert.True(endHotspot < startHotspot * 0.25f,
            $"Expected strong decay through base-connected tunnel. startHotspot={startHotspot:0.00}, endHotspot={endHotspot:0.00}");
        Assert.True(endHotspot <= endBaseTemperature + 12f,
            $"Expected hotspot to converge near base temperature. endHotspot={endHotspot:0.00}, endBaseTemperature={endBaseTemperature:0.00}, startBaseTemperature={startBaseTemperature:0.00}");
    }

    [Fact]
    public void WorldAdvance_NoExternalHeating_SystemEnergyDoesNotIncreasePerTick()
    {
        var world = TestHelpers.CreateSeededWorld(seed: 9595);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_NoExternalHeating_SystemEnergyDoesNotIncreasePerTick));

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
        var world = TestHelpers.CreateSeededWorld(seed: TestHelpers.DefaultSeed);
        using var visual = TestVisualTrace.Start(nameof(WorldAdvance_GameLikeInputs_NoTankSettlesNearOneTwenty));
        var tanks = world.TankList.Tanks;
        var bot = new BotTankAI(seed: TestHelpers.DefaultSeed + 2);

        const float plateauCenter = 120f;
        const float plateauHalfBand = 10f;
        int totalFrames = Tweaks.Perf.TargetFps * 75;
        int tailWindow = Tweaks.Perf.TargetFps * 15;

        var tailByTank = new Queue<float>[tanks.Count];
        for (int i = 0; i < tanks.Count; i++)
            tailByTank[i] = new Queue<float>(tailWindow);

        for (int frame = 0; frame < totalFrames; frame++)
        {
            world.Advance(i =>
            {
                if (i == 0)
                    return default;

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

        var impactPos = basePos + new Offset(0, 48);
        CarveVerticalLine(world.Terrain, basePos, startDy: 0, endDyInclusive: 46);
        world.Terrain.SetPixel(impactPos, TerrainPixel.Rock);
        world.Terrain.SetPixel(impactPos + new Offset(1, 0), TerrainPixel.Rock);
        world.Terrain.SetPixel(impactPos + new Offset(-1, 0), TerrainPixel.Rock);

        float hotspotAtStart = world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f;

        var spawnPos = impactPos + new Offset(0, -12);
        world.Projectiles.Add(Tunnerer.Core.Entities.Projectiles.Projectile.CreateBullet(
            spawnPos,
            new VectorF(0f, Tweaks.Weapon.CannonBulletSpeed),
            tank.Color));

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

        var impactPos = basePos + new Offset(0, 46);
        CarveVerticalLine(world.Terrain, basePos, startDy: 0, endDyInclusive: 48);

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

        const int shootFrames = 72;
        AdvanceWorld(world, frames: shootFrames, frameInputForTank0: _ => shootDown, visual: visual, phase: "shoot", captureEvery: 6);
        float hotspotAfterShooting = world.Terrain.SampleAverageHeat(impactPos, radius: 6) * 255f;

        const int waitFrames = 24 * 12;
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

        int firstOutsideDy = (Tweaks.Base.BaseSize / 2) + 1;
        CarveVerticalLine(world.Terrain, basePos, firstOutsideDy, firstOutsideDy + 56);

        var driveDown = MoveAndAim(0, 1, 0f, 1f);
        var driveUp = MoveAndAim(0, -1, 0f, -1f);

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
}
