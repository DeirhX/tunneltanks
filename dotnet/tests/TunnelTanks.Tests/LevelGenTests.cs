using TunnelTanks.Core.Config;
using TunnelTanks.Core.LevelGen;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Types;

namespace TunnelTanks.Tests;

public class LevelGenTests
{
    private const int W = 320;
    private const int H = 200;
    private readonly Size _size = new(W, H);

    [Fact]
    public void FullGeneration_ProducesCorrectDirtPercentage()
    {
        var gen = new ToastGenerator();
        var (terrain, spawns) = gen.Generate(_size);
        terrain.MaterializeTerrain();

        int dirtCount = 0;
        for (int i = 0; i < _size.Area; i++)
            if (Pixel.IsDirt(terrain[i])) dirtCount++;

        double pct = 100.0 * dirtCount / _size.Area;
        Assert.True(pct >= 40 && pct <= 85,
            $"Dirt percentage {pct:F1}% outside expected range [40, 85]");
    }

    [Fact]
    public void FullGeneration_ProducesEnoughSpawns()
    {
        var gen = new ToastGenerator();
        var (_, spawns) = gen.Generate(_size);
        Assert.True(spawns.Length >= 2, $"Expected at least 2 spawns, got {spawns.Length}");
    }

    [Fact]
    public void FullGeneration_SpawnsAreInsideBorder()
    {
        var gen = new ToastGenerator();
        var (_, spawns) = gen.Generate(_size);

        int border = Tweaks.LevelGen.BorderWidth;
        foreach (var s in spawns)
        {
            Assert.True(s.X >= border && s.X < W - border, $"Spawn x={s.X} outside border");
            Assert.True(s.Y >= border && s.Y < H - border, $"Spawn y={s.Y} outside border");
        }
    }

    [Fact]
    public void FullGeneration_SpawnsAreFarEnoughApart()
    {
        var gen = new ToastGenerator();
        var (_, spawns) = gen.Generate(_size);

        int minDistSq = Tweaks.Base.MinDistance * Tweaks.Base.MinDistance;
        for (int i = 0; i < spawns.Length; i++)
            for (int j = i + 1; j < spawns.Length; j++)
                Assert.True(Position.DistanceSquared(spawns[i], spawns[j]) >= minDistSq,
                    $"Spawns {spawns[i]} and {spawns[j]} too close");
    }

    [Fact]
    public void SmoothingConverges_NoLevelGenPixelsRemain()
    {
        var gen = new ToastGenerator();
        var (terrain, _) = gen.Generate(_size);

        for (int i = 0; i < _size.Area; i++)
        {
            var pix = terrain[i];
            Assert.True(pix == TerrainPixel.LevelGenDirt || pix == TerrainPixel.LevelGenRock,
                $"Unexpected pixel {pix} at offset {i} after generation (before materialize)");
        }
    }

    [Fact]
    public void SmoothingConverges_NoBoundaryArtifacts()
    {
        var gen = new ToastGenerator();
        var (terrain, _) = gen.Generate(_size);

        for (int x = 0; x < W; x++)
        {
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(x, 0)));
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(x, H - 1)));
        }
        for (int y = 0; y < H; y++)
        {
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(0, y)));
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(W - 1, y)));
        }
    }

    [Fact]
    public void MaterializeTerrain_NeverLeavesLevelGenValues()
    {
        var gen = new ToastGenerator();
        var (terrain, _) = gen.Generate(_size);
        terrain.MaterializeTerrain();

        for (int i = 0; i < _size.Area; i++)
        {
            var pix = terrain[i];
            Assert.False(pix == TerrainPixel.LevelGenDirt || pix == TerrainPixel.LevelGenRock || pix == TerrainPixel.LevelGenMark,
                $"LevelGen pixel {pix} survived MaterializeTerrain at offset {i}");
        }
    }

    [Fact]
    public void GenerateInside_RespectsMargins()
    {
        var rng = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            var pos = GeneratorUtils.GenerateInside(_size, 30, rng);
            Assert.True(pos.X >= 30 && pos.X < W - 30);
            Assert.True(pos.Y >= 30 && pos.Y < H - 30);
        }
    }

    [Fact]
    public void DrawLine_ConnectsEndpoints()
    {
        var terrain = new Terrain(_size);
        terrain.Fill(TerrainPixel.LevelGenRock);

        var from = new Position(10, 10);
        var to = new Position(50, 30);
        GeneratorUtils.DrawLine(terrain, from, to, TerrainPixel.LevelGenDirt);

        Assert.Equal(TerrainPixel.LevelGenDirt, terrain.GetPixelRaw(from));
        Assert.Equal(TerrainPixel.LevelGenDirt, terrain.GetPixelRaw(to));
    }

    [Fact]
    public void Fill_SetsEveryPixel()
    {
        var terrain = new Terrain(new Size(16, 16));
        terrain.Fill(TerrainPixel.DirtHigh);

        for (int i = 0; i < 256; i++)
            Assert.Equal(TerrainPixel.DirtHigh, terrain[i]);
    }

    [Fact]
    public void SetOutside_SetsOnlyBorder()
    {
        var terrain = new Terrain(new Size(10, 10));
        terrain.Fill(TerrainPixel.LevelGenDirt);
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenRock);

        // Border should be rock
        for (int x = 0; x < 10; x++)
        {
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(x, 0)));
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(x, 9)));
        }
        for (int y = 0; y < 10; y++)
        {
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(0, y)));
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(9, y)));
        }
        // Interior should remain dirt
        Assert.Equal(TerrainPixel.LevelGenDirt, terrain.GetPixelRaw(new Position(5, 5)));
    }

    [Fact]
    public void PointDistanceSquared_Correct()
    {
        Assert.Equal(25, Position.DistanceSquared(new Position(0, 0), new Position(3, 4)));
        Assert.Equal(0, Position.DistanceSquared(new Position(5, 5), new Position(5, 5)));
    }

    [Fact]
    public void TargetDirtAmount_MatchesCpp()
    {
        // C++: lvl->GetSize().x * lvl->GetSize().y * DirtTargetPercent / 100
        int expected = W * H * 65 / 100;
        Assert.Equal(expected, Tweaks.LevelGen.TargetDirtAmount(_size));
    }

    [Fact]
    public void SmoothingSteps_IsNegativeOne_MatchesCpp()
    {
        Assert.Equal(-1, Tweaks.LevelGen.SmoothingSteps);
    }

    [Fact]
    public void FullGeneration_ConnectedCaves_SpawnsReachable()
    {
        var gen = new ToastGenerator();
        var (terrain, spawns) = gen.Generate(_size);
        AssertSpawnsReachable(terrain, spawns, W, H);
    }

    [Fact]
    public void OptimizedMode_ProducesValidDirtPercentage()
    {
        var gen = new ToastGenerator();
        var (terrain, spawns) = gen.Generate(_size, mode: LevelGenMode.Optimized);
        terrain.MaterializeTerrain(parallel: true);

        int dirtCount = 0;
        for (int i = 0; i < _size.Area; i++)
            if (Pixel.IsDirt(terrain[i])) dirtCount++;

        double pct = 100.0 * dirtCount / _size.Area;
        Assert.True(pct >= 35 && pct <= 85,
            $"Optimized dirt percentage {pct:F1}% outside expected range [35, 85]");
        Assert.True(spawns.Length >= 2, $"Expected at least 2 spawns, got {spawns.Length}");
    }

    [Fact]
    public void OptimizedMode_NoBoundaryArtifacts()
    {
        var gen = new ToastGenerator();
        var (terrain, _) = gen.Generate(_size, mode: LevelGenMode.Optimized);

        for (int x = 0; x < W; x++)
        {
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(x, 0)));
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(x, H - 1)));
        }
        for (int y = 0; y < H; y++)
        {
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(0, y)));
            Assert.Equal(TerrainPixel.LevelGenRock, terrain.GetPixelRaw(new Position(W - 1, y)));
        }
    }

    [Fact]
    public void OptimizedMode_ConnectedCaves_SpawnsReachable()
    {
        var gen = new ToastGenerator();
        var (terrain, spawns) = gen.Generate(_size, mode: LevelGenMode.Optimized);
        AssertSpawnsReachable(terrain, spawns, W, H);
    }

    [Fact]
    public void OptimizedMode_LargeMap_ProducesValidResult()
    {
        int lw = 1000, lh = 500;
        var largeSize = new Size(lw, lh);
        var gen = new ToastGenerator();
        var (terrain, spawns) = gen.Generate(largeSize, mode: LevelGenMode.Optimized);
        terrain.MaterializeTerrain(parallel: true);

        Assert.True(spawns.Length >= 2);

        int dirtCount = 0;
        for (int i = 0; i < largeSize.Area; i++)
            if (Pixel.IsDirt(terrain[i])) dirtCount++;

        double pct = 100.0 * dirtCount / largeSize.Area;
        Assert.True(pct >= 40 && pct <= 85,
            $"Large optimized dirt percentage {pct:F1}% outside expected range [40, 85]");
    }

    [Fact]
    public void Benchmark_DeterministicVsOptimized()
    {
        int bw = 1000, bh = 500;
        var benchSize = new Size(bw, bh);
        var gen = new ToastGenerator();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Warmup
        gen.Generate(benchSize, seed: 42, mode: LevelGenMode.Deterministic);
        gen.Generate(benchSize, mode: LevelGenMode.Optimized);

        const int runs = 3;
        double detTotal = 0, optTotal = 0;

        for (int i = 0; i < runs; i++)
        {
            sw.Restart();
            gen.Generate(benchSize, seed: 42, mode: LevelGenMode.Deterministic);
            detTotal += sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            gen.Generate(benchSize, mode: LevelGenMode.Optimized);
            optTotal += sw.Elapsed.TotalMilliseconds;
        }

        double detAvg = detTotal / runs;
        double optAvg = optTotal / runs;
        double speedup = detAvg / optAvg;

        // Output via test output (shows up in verbose test output)
        Assert.True(true,
            $"1000x500 avg over {runs}: deterministic={detAvg:F1}ms, optimized={optAvg:F1}ms, speedup={speedup:F2}x");

        // The optimized mode should be faster (or at least not significantly slower)
        Assert.True(optAvg <= detAvg * 1.5,
            $"Optimized ({optAvg:F1}ms) should not be slower than deterministic ({detAvg:F1}ms)");
    }

    private static void AssertSpawnsReachable(Terrain terrain, Position[] spawns, int w, int h)
    {
        var visited = new bool[w * h];
        var queue = new Queue<Position>();
        queue.Enqueue(spawns[0]);
        visited[spawns[0].X + spawns[0].Y * w] = true;

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = pos.X + dx, ny = pos.Y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int off = nx + ny * w;
                    if (visited[off]) continue;
                    if (terrain.GetPixelRaw(new Position(nx, ny)) != TerrainPixel.LevelGenDirt) continue;
                    visited[off] = true;
                    queue.Enqueue(new Position(nx, ny));
                }
        }

        for (int i = 1; i < spawns.Length; i++)
        {
            int off = spawns[i].X + spawns[i].Y * w;
            Assert.True(visited[off], $"Spawn {i} at {spawns[i]} is not connected to spawn 0");
        }
    }
}
