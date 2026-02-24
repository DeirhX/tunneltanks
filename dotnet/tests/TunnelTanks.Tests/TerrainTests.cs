using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Types;

namespace TunnelTanks.Tests;

public class TerrainTests
{
    private Terrain MakeTerrain(int w, int h) => new(new Size(w, h));

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
}
