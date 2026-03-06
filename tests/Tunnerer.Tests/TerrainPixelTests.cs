using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

public class TerrainPixelTests
{
    [Theory]
    [InlineData(TerrainPixel.LevelGenDirt, 0)]
    [InlineData(TerrainPixel.LevelGenRock, 1)]
    [InlineData(TerrainPixel.LevelGenMark, 2)]
    [InlineData(TerrainPixel.Blank, (byte)' ')]
    [InlineData(TerrainPixel.DirtHigh, (byte)'D')]
    [InlineData(TerrainPixel.DirtGrow, (byte)'g')]
    [InlineData(TerrainPixel.Rock, (byte)'r')]
    [InlineData(TerrainPixel.DecalHigh, (byte)'.')]
    [InlineData(TerrainPixel.DecalLow, (byte)',')]
    [InlineData(TerrainPixel.BaseMin, (byte)'0')]
    [InlineData(TerrainPixel.BaseMax, (byte)'7')]
    [InlineData(TerrainPixel.BaseBarrier, (byte)'8')]
    [InlineData(TerrainPixel.BaseCore, (byte)'9')]
    [InlineData(TerrainPixel.ConcreteLow, (byte)'c')]
    [InlineData(TerrainPixel.ConcreteHigh, (byte)'C')]
    public void EnumValues_MatchCpp(TerrainPixel pixel, byte expected)
    {
        Assert.Equal(expected, (byte)pixel);
    }

    [Theory]
    [InlineData(TerrainPixel.DirtHigh, true)]
    [InlineData(TerrainPixel.DirtGrow, false)]
    [InlineData(TerrainPixel.Rock, false)]
    [InlineData(TerrainPixel.Blank, false)]
    public void IsDirt_MatchesCpp(TerrainPixel pixel, bool expected)
    {
        Assert.Equal(expected, Pixel.IsDirt(pixel));
    }

    [Theory]
    [InlineData(TerrainPixel.DirtHigh, true)]
    [InlineData(TerrainPixel.DirtGrow, true)]
    [InlineData(TerrainPixel.Rock, false)]
    [InlineData(TerrainPixel.Blank, false)]
    public void IsDiggable_MatchesCpp(TerrainPixel pixel, bool expected)
    {
        Assert.Equal(expected, Pixel.IsDiggable(pixel));
    }

    [Theory]
    [InlineData(TerrainPixel.DecalHigh, true)]
    [InlineData(TerrainPixel.DecalLow, true)]
    [InlineData(TerrainPixel.Blank, false)]
    [InlineData(TerrainPixel.DirtHigh, false)]
    public void IsScorched_MatchesCpp(TerrainPixel pixel, bool expected)
    {
        Assert.Equal(expected, Pixel.IsScorched(pixel));
    }

    [Theory]
    [InlineData(TerrainPixel.Rock, true)]
    [InlineData(TerrainPixel.ConcreteHigh, true)]
    [InlineData(TerrainPixel.ConcreteLow, true)]
    [InlineData(TerrainPixel.BaseMin, true)]
    [InlineData(TerrainPixel.DirtHigh, false)]
    [InlineData(TerrainPixel.Blank, false)]
    public void IsBlockingCollision_MatchesCpp(TerrainPixel pixel, bool expected)
    {
        Assert.Equal(expected, Pixel.IsBlockingCollision(pixel));
    }

    [Fact]
    public void GetColor_ProducesNonMagentaForAllKnownPixels()
    {
        var magenta = new Color(255, 0, 255);
        TerrainPixel[] knownPixels =
        [
            TerrainPixel.Blank, TerrainPixel.DirtHigh,
            TerrainPixel.DirtGrow, TerrainPixel.Rock, TerrainPixel.DecalHigh,
            TerrainPixel.DecalLow, TerrainPixel.ConcreteLow, TerrainPixel.ConcreteHigh,
            TerrainPixel.EnergyLow, TerrainPixel.EnergyMedium, TerrainPixel.EnergyHigh,
            TerrainPixel.BaseMin, TerrainPixel.BaseBarrier, TerrainPixel.BaseCore
        ];

        foreach (var pix in knownPixels)
            Assert.NotEqual(magenta, Pixel.GetColor(pix));
    }
}
