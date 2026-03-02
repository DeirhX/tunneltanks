using Tunnerer.Core.Terrain;
using Tunnerer.Core.Config;
using Tunnerer.Core.Thermal;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

public class TerrainTests
{
    private TerrainGrid MakeTerrain(int w, int h) => new(new Size(w, h));

    [Fact]
    public void SetPixelRaw_GetPixelRaw_Roundtrip()
    {
        var t = MakeTerrain(10, 10);
        var pos = new Position(5, 5);
        t.SetPixelRaw(pos, TerrainPixel.DirtHigh);
        Assert.Equal(TerrainPixel.DirtHigh, t.GetPixelRaw(pos));
    }

    [Fact]
    public void GetPixel_OutOfBounds_ReturnsRock()
    {
        var t = MakeTerrain(10, 10);
        Assert.Equal(TerrainPixel.Rock, t.GetPixel(new Position(-1, 0)));
        Assert.Equal(TerrainPixel.Rock, t.GetPixel(new Position(0, 10)));
        Assert.Equal(TerrainPixel.Rock, t.GetPixel(new Position(10, 5)));
    }

    [Fact]
    public void SetPixel_AddsToChangeList()
    {
        var t = MakeTerrain(10, 10);
        Assert.Empty(t.GetChangeList());

        t.SetPixel(new Position(3, 3), TerrainPixel.DirtLow);
        Assert.Single(t.GetChangeList());
        Assert.Equal(new Position(3, 3), t.GetChangeList()[0]);
    }

    [Fact]
    public void ClearChangeList_EmptiesIt()
    {
        var t = MakeTerrain(10, 10);
        t.SetPixel(new Position(3, 3), TerrainPixel.DirtLow);
        t.ClearChangeList();
        Assert.Empty(t.GetChangeList());
    }

    [Fact]
    public void CountLevelGenNeighbors_AllRock_Returns8()
    {
        var t = MakeTerrain(5, 5);
        for (int i = 0; i < 25; i++)
            t[i] = TerrainPixel.LevelGenRock;
        Assert.Equal(8, t.CountLevelGenNeighbors(new Position(2, 2)));
    }

    [Fact]
    public void CountLevelGenNeighbors_AllDirt_Returns0()
    {
        var t = MakeTerrain(5, 5);
        for (int i = 0; i < 25; i++)
            t[i] = TerrainPixel.LevelGenDirt;
        Assert.Equal(0, t.CountLevelGenNeighbors(new Position(2, 2)));
    }

    [Fact]
    public void CountLevelGenNeighbors_MixedPattern()
    {
        var t = MakeTerrain(5, 5);
        for (int i = 0; i < 25; i++)
            t[i] = TerrainPixel.LevelGenDirt;

        // Set 3 neighbors to rock
        t[1, 1] = TerrainPixel.LevelGenRock; // top-left of (2,2)
        t[2, 1] = TerrainPixel.LevelGenRock; // top of (2,2)
        t[3, 1] = TerrainPixel.LevelGenRock; // top-right of (2,2)

        Assert.Equal(3, t.CountLevelGenNeighbors(new Position(2, 2)));
    }

    [Fact]
    public void CountDirtNeighbors_Matches()
    {
        var t = MakeTerrain(5, 5);
        for (int i = 0; i < 25; i++)
            t[i] = TerrainPixel.Blank;

        t[1, 1] = TerrainPixel.DirtHigh;
        t[2, 1] = TerrainPixel.DirtLow;
        t[3, 1] = TerrainPixel.DirtGrow; // not counted as dirt
        t[1, 2] = TerrainPixel.DirtHigh;

        Assert.Equal(3, t.CountDirtNeighbors(new Position(2, 2)));
    }

    [Fact]
    public void HasLevelGenNeighbor_DetectsAdjacentDirt()
    {
        var t = MakeTerrain(5, 5);
        for (int i = 0; i < 25; i++)
            t[i] = TerrainPixel.LevelGenRock;

        Assert.False(t.HasLevelGenNeighbor(2, 2));

        t[1, 1] = TerrainPixel.LevelGenDirt;
        Assert.True(t.HasLevelGenNeighbor(2, 2));
    }

    [Fact]
    public void MaterializeTerrain_ConvertsLevelGenToGamePixels()
    {
        var t = MakeTerrain(10, 10);
        for (int i = 0; i < 100; i++)
            t[i] = i % 2 == 0 ? TerrainPixel.LevelGenDirt : TerrainPixel.LevelGenRock;

        t.MaterializeTerrain();

        for (int i = 0; i < 100; i++)
        {
            if (i % 2 == 0)
                Assert.True(Pixel.IsDirt(t[i]), $"Expected dirt at offset {i}, got {t[i]}");
            else
                Assert.Equal(TerrainPixel.Rock, t[i]);
        }
    }

    [Fact]
    public void DrawAllToSurface_FillsSurface()
    {
        var t = MakeTerrain(4, 4);
        for (int i = 0; i < 16; i++)
            t[i] = TerrainPixel.DirtHigh;

        var surface = new uint[16];
        t.DrawAllToSurface(surface);

        uint expected = Pixel.GetColor(TerrainPixel.DirtHigh).ToArgb();
        Assert.All(surface, px => Assert.Equal(expected, px));
    }

    [Fact]
    public void DrawChangesToSurface_OnlyUpdatesChanged()
    {
        var t = MakeTerrain(4, 4);
        for (int i = 0; i < 16; i++)
            t[i] = TerrainPixel.Blank;

        var surface = new uint[16];
        t.DrawAllToSurface(surface);

        uint blankColor = Pixel.GetColor(TerrainPixel.Blank).ToArgb();
        uint dirtColor = Pixel.GetColor(TerrainPixel.DirtHigh).ToArgb();

        t.SetPixel(new Position(1, 1), TerrainPixel.DirtHigh);
        t.DrawChangesToSurface(surface);

        Assert.Equal(dirtColor, surface[1 + 1 * 4]);
        Assert.Equal(blankColor, surface[0]); // unchanged
    }

    [Fact]
    public void IndexerByOffset_MatchesPosition()
    {
        var t = MakeTerrain(10, 8);
        t.SetPixelRaw(new Position(3, 5), TerrainPixel.Rock);
        Assert.Equal(TerrainPixel.Rock, t[3 + 5 * 10]);
    }

    [Fact]
    public void MaterialHeatExchange_AndAmbient_DriveTowardEquilibrium()
    {
        var t = MakeTerrain(16, 16);
        for (int i = 0; i < 16 * 16; i++)
            t[i] = TerrainPixel.Rock;

        var hot = new Position(8, 8);
        t.AddHeat(hot, 255);

        byte startCenter = t.GetHeat(hot);
        for (int i = 0; i < 200; i++)
            t.CoolDown(Tweaks.World.HeatCooldownPerTick, Tweaks.World.HeatDiffuseRate);

        byte endCenter = t.GetHeat(hot);
        byte ambient = (byte)Math.Clamp((int)MathF.Round(Tweaks.World.ThermalAmbientTemperature), 0, 255);

        Assert.True(endCenter < startCenter, $"Expected hot spot to cool from {startCenter}, got {endCenter}");
        Assert.InRange(endCenter, (byte)Math.Max(0, ambient - 10), (byte)Math.Min(255, ambient + 30));
    }

    [Fact]
    public void CoolDown_NoSources_FirstTick_DoesNotIncreaseTotalHeat()
    {
        var t = MakeTerrain(20, 20);
        for (int i = 0; i < 20 * 20; i++)
            t[i] = TerrainPixel.Rock;

        var hot = new Position(10, 10);
        t.AddHeatRadius(hot, 255, 6);

        int startTotal = SumHeat(t);
        t.CoolDown(Tweaks.World.HeatCooldownPerTick, Tweaks.World.HeatDiffuseRate);
        int afterFirstTick = SumHeat(t);
        Assert.True(afterFirstTick <= startTotal,
            $"Expected first CoolDown tick to be non-increasing. start={startTotal}, tick1={afterFirstTick}");
    }

    [Fact]
    public void MaterialHeatExchange_NoSources_TotalHeatNeverIncreasesPerTick()
    {
        var t = MakeTerrain(20, 20);
        for (int i = 0; i < 20 * 20; i++)
            t[i] = TerrainPixel.Rock;

        var hot = new Position(10, 10);
        t.AddHeatRadius(hot, 255, 6);
        int previousTotal = SumHeat(t);
        int startTotal = previousTotal;
        int maxUpstep = 0;

        for (int i = 0; i < 300; i++)
        {
            t.CoolDown(Tweaks.World.HeatCooldownPerTick, Tweaks.World.HeatDiffuseRate);
            int currentTotal = SumHeat(t);
            int upstep = currentTotal - previousTotal;
            if (upstep > maxUpstep)
                maxUpstep = upstep;

            Assert.True(currentTotal <= previousTotal,
                $"Expected non-increasing total heat at tick {i}. prev={previousTotal}, current={currentTotal}");
            previousTotal = currentTotal;
        }

        int endTotal = previousTotal;
        Assert.True(endTotal <= startTotal, $"Expected non-increasing total heat. start={startTotal}, end={endTotal}");
        Assert.Equal(0, maxUpstep);
    }

    [Fact]
    public void TerrainHeatEngine_OneDimensionalStrip_ClosedSystem_ConvergesToInitialEnergyMean()
    {
        // Single-pixel-wide bed (1 x N): inject heat on one side and wait for equilibrium.
        const int width = 1;
        const int height = 64;
        var engine = new TerrainHeatEngine();
        var heat = new byte[width * height];
        var pixels = new TerrainPixel[width * height];
        byte ambient = (byte)Math.Clamp((int)MathF.Round(Tweaks.World.ThermalAmbientTemperature), 0, 255);

        for (int i = 0; i < heat.Length; i++)
        {
            heat[i] = ambient;
            pixels[i] = TerrainPixel.Rock;
        }

        // Spawn heat at one side of the strip.
        heat[0] = 220;
        int startTotal = SumHeat(heat);
        double startInternalEnergy = SumThermalEnergy(heat, pixels);
        float expectedMean = startTotal / (float)heat.Length;

        int? firstInternalDivergenceStep = null;
        double firstInternalDivergenceEnergy = 0.0;
        const double internalEnergyTolerance = 0.25;
        for (int step = 1; step <= 30000; step++)
        {
            engine.Step(heat, pixels, width, height, includeAmbientExchange: false);
            double internalEnergy = engine.SumInternalEnergy(pixels);
            if (!firstInternalDivergenceStep.HasValue &&
                Math.Abs(internalEnergy - startInternalEnergy) > internalEnergyTolerance)
            {
                firstInternalDivergenceStep = step;
                firstInternalDivergenceEnergy = internalEnergy;
            }
        }

        int min = 255, max = 0, sum = 0;
        for (int i = 0; i < heat.Length; i++)
        {
            int v = heat[i];
            if (v < min) min = v;
            if (v > max) max = v;
            sum += v;
        }

        Assert.True(!firstInternalDivergenceStep.HasValue,
            $"Internal-energy diverged at step={firstInternalDivergenceStep}, " +
            $"internal={firstInternalDivergenceEnergy:0.0000}, startInternal={startInternalEnergy:0.0000}");
        float mean = sum / (float)heat.Length;
        Assert.InRange(mean, expectedMean - 0.75f, expectedMean + 0.75f);
        Assert.InRange(min, (int)MathF.Floor(expectedMean) - 3, (int)MathF.Ceiling(expectedMean) + 3);
        Assert.InRange(max, (int)MathF.Floor(expectedMean) - 3, (int)MathF.Ceiling(expectedMean) + 3);
    }

    [Fact]
    public void TerrainHeatEngine_SquareClosedSystem_ConvergesToInitialEnergyMean()
    {
        const int width = 32;
        const int height = 32;
        var engine = new TerrainHeatEngine();
        var heat = new byte[width * height];
        var pixels = new TerrainPixel[width * height];
        byte ambient = (byte)Math.Clamp((int)MathF.Round(Tweaks.World.ThermalAmbientTemperature), 0, 255);

        for (int i = 0; i < heat.Length; i++)
        {
            heat[i] = ambient;
            pixels[i] = TerrainPixel.Rock;
        }

        // Inject a hot spot in the center and verify the closed system equilibrates to
        // the mean implied by initial total energy.
        int cx = width / 2;
        int cy = height / 2;
        heat[cx + cy * width] = 255;

        int startTotal = SumHeat(heat);
        double startInternalEnergy = SumThermalEnergy(heat, pixels);
        float expectedMean = startTotal / (float)heat.Length;

        int? firstInternalDivergenceStep = null;
        double firstInternalDivergenceEnergy = 0.0;
        const double internalEnergyTolerance = 0.25;
        for (int step = 1; step <= 60000; step++)
        {
            engine.Step(heat, pixels, width, height, includeAmbientExchange: false);
            double internalEnergy = engine.SumInternalEnergy(pixels);
            if (!firstInternalDivergenceStep.HasValue &&
                Math.Abs(internalEnergy - startInternalEnergy) > internalEnergyTolerance)
            {
                firstInternalDivergenceStep = step;
                firstInternalDivergenceEnergy = internalEnergy;
            }
        }

        int min = 255, max = 0, sum = 0;
        for (int i = 0; i < heat.Length; i++)
        {
            int v = heat[i];
            if (v < min) min = v;
            if (v > max) max = v;
            sum += v;
        }

        Assert.True(!firstInternalDivergenceStep.HasValue,
            $"Internal-energy diverged at step={firstInternalDivergenceStep}, " +
            $"internal={firstInternalDivergenceEnergy:0.0000}, startInternal={startInternalEnergy:0.0000}");
        float mean = sum / (float)heat.Length;
        Assert.InRange(mean, expectedMean - 0.5f, expectedMean + 0.5f);
        Assert.InRange(min, (int)MathF.Floor(expectedMean) - 2, (int)MathF.Ceiling(expectedMean) + 2);
        Assert.InRange(max, (int)MathF.Floor(expectedMean) - 2, (int)MathF.Ceiling(expectedMean) + 2);
    }

    private static int SumHeat(byte[] heat)
    {
        int sum = 0;
        for (int i = 0; i < heat.Length; i++)
            sum += heat[i];
        return sum;
    }

    private static double SumThermalEnergy(byte[] heat, TerrainPixel[] pixels)
    {
        double sum = 0.0;
        for (int i = 0; i < heat.Length; i++)
            sum += heat[i] * ThermalCapacityFor(Pixel.GetThermalMaterial(pixels[i]));
        return sum;
    }

    private static float ThermalCapacityFor(ThermalMaterial material) => material switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalCapacityAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalCapacityDirt,
        _ => Tweaks.World.ThermalCapacityStone,
    };

    private static int SumHeat(TerrainGrid terrain)
    {
        int sum = 0;
        for (int i = 0; i < terrain.Size.Area; i++)
            sum += terrain.GetHeat(i);
        return sum;
    }

}
