namespace TunnelTanks.Tests;

using TunnelTanks.Core;
using TunnelTanks.Core.LevelGen;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Types;

internal static class TestHelpers
{
    public const int DefaultSeed = 42;
    public static readonly Size DefaultMapSize = new(320, 200);

    public static World CreateSeededWorld(Size? size = null, int seed = DefaultSeed)
    {
        var mapSize = size ?? DefaultMapSize;
        var gen = new ToastGenerator();
        var (terrain, spawns) = gen.Generate(mapSize, seed: seed);
        var world = new World(mapSize);
        world.Initialize(terrain, spawns, materializeSeed: seed + 1);
        return world;
    }

    public static double CountDirtPercentage(TerrainGrid terrain, Size size)
    {
        int dirtCount = 0;
        for (int i = 0; i < size.Area; i++)
            if (Pixel.IsDirt(terrain[i])) dirtCount++;
        return 100.0 * dirtCount / size.Area;
    }

    public static void AssertBorderIsRock(TerrainGrid terrain, int w, int h)
    {
        for (int x = 0; x < w; x++)
        {
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(x, 0)));
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(x, h - 1)));
        }
        for (int y = 0; y < h; y++)
        {
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(0, y)));
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(w - 1, y)));
        }
    }

    public static void AssertNoLevelGenPixels(TerrainGrid terrain, Size size)
    {
        for (int i = 0; i < size.Area; i++)
        {
            var pix = terrain[i];
            Assert.False(pix == TerrainPixel.LevelGenDirt || pix == TerrainPixel.LevelGenRock || pix == TerrainPixel.LevelGenMark,
                $"LevelGen pixel {pix} survived at offset {i}");
        }
    }
}
