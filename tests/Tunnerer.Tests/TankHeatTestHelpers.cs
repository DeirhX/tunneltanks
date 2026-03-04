using Tunnerer.Core;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Input;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

internal static class TankHeatTestHelpers
{
    internal static readonly object StoneAmbientToggleGate = new();
    internal static readonly ControllerOutput IdleInput = default;

    internal static (World world, Tank tank) CreateWorldWithPrimaryTank()
    {
        var world = TestHelpers.CreateSeededWorld();
        return (world, world.TankList.Tanks[0]);
    }

    internal static (World world, Tank tank) CreateWorldWithPrimaryTank(int seed, bool enableTerrainRegrowth = true)
    {
        var world = TestHelpers.CreateSeededWorld(seed: seed, enableTerrainRegrowth: enableTerrainRegrowth);
        return (world, world.TankList.Tanks[0]);
    }

    internal static Position GetWorldCenter(World world) =>
        new(world.Terrain.Width / 2, world.Terrain.Height / 2);

    internal static Position RequireBasePosition(Tank tank)
    {
        Assert.NotNull(tank.Base);
        return tank.Base!.Position;
    }

    internal static void SetTankHeat(Tank tank, float heat)
    {
        tank.Heat = heat;
        tank.Reactor.Current.Heat = new Core.Resources.Heat((int)MathF.Round(heat));
    }

    internal static ControllerOutput MoveAndAim(int moveX, int moveY, float aimX, float aimY) =>
        new()
        {
            MoveSpeed = new Offset(moveX, moveY),
            AimDirection = new DirectionF(aimX, aimY),
        };

    internal static void CarveVerticalLine(TerrainGrid terrain, Position anchor, int startDy, int endDyInclusive)
    {
        for (int i = startDy; i <= endDyInclusive; i++)
        {
            var pos = anchor + new Offset(0, i);
            if (terrain.IsInside(pos))
                terrain.SetPixel(pos, TerrainPixel.Blank);
        }
    }

    internal static void AdvanceTank(
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

    internal static void AdvanceWorld(
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

    internal static void AdvanceWorld(
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

    internal static void ClearArea(TerrainGrid terrain, Position center, int radius)
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

    internal static void CoolAreaToZero(TerrainGrid terrain, Position center, int radius)
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

    internal static void SetupHotZoneScenario(World world, Tank tank, Position zonePos, float initialHeat)
    {
        tank.Position = zonePos;
        ClearArea(world.Terrain, zonePos, radius: 6);
        world.Terrain.AddHeatRadius(zonePos, amount: 255, radius: 6);
        SetTankHeat(tank, initialHeat);
    }

    internal static double SumSystemThermalEnergy(World world)
    {
        double total = 0.0;
        total += world.Terrain.SumTotalThermalEnergy();
        foreach (var tank in world.TankList.Tanks)
            total += tank.Heat * Tweaks.Tank.TankHeatCapacity;
        foreach (var tankBase in world.TankBases.Bases)
            total += (int)tankBase.Reactor.Heat * Tweaks.World.ThermalCapacityBase;
        return total;
    }

    internal static void AdvanceUntil(World world, int maxFrames, Func<bool> done, TestVisualTrace? visual = null, string phase = "advance")
    {
        for (int i = 0; i < maxFrames; i++)
        {
            if (done())
                return;
            world.Advance(_ => default);
            visual?.Capture(world, $"{phase}_{i:D4}");
        }
    }

    internal static double SumTerrainThermalEnergy(TerrainGrid terrain)
    {
        double total = 0.0;
        for (int i = 0; i < terrain.Size.Area; i++)
        {
            float capacity = ThermalCapacityFor(terrain.GetPixelRaw(i));
            total += terrain.GetHeatTemperature(i) * capacity;
            total += terrain.GetAirTemperature(i) * Tweaks.World.ThermalCapacityAir;
        }

        return total;
    }

    internal static float ThermalCapacityFor(TerrainPixel pixel) => Pixel.GetThermalMaterial(pixel) switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalCapacityAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalCapacityDirt,
        ThermalMaterial.Base => Tweaks.World.ThermalCapacityBase,
        _ => Tweaks.World.ThermalCapacityStone,
    };

    internal static float StdDev(IEnumerable<float> values, float mean)
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
