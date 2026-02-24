namespace TunnelTanks.Core.LevelGen;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Config;

public enum LevelGenMode
{
    Deterministic,
    Optimized,
}

public class ToastGenerator
{
    public (TerrainGrid terrain, Position[] spawns) Generate(Size size, int? seed = null,
        LevelGenMode mode = LevelGenMode.Deterministic)
    {
        var terrain = new TerrainGrid(size);
        terrain.Fill(TerrainPixel.LevelGenRock);

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var spawns = GenerateTree(terrain, rng);

        if (mode == LevelGenMode.Optimized)
        {
            GenerateOptimized(terrain, spawns);
        }
        else
        {
            RandomlyExpand(terrain, rng);
            SmoothCavern(terrain);
        }

        return (terrain, spawns);
    }

    /// <summary>
    /// Generates random points, selects spawn positions, then builds a minimum spanning
    /// tree (Kruskal's algorithm with naive union-find) connecting them via dirt tunnels.
    /// </summary>
    private static Position[] GenerateTree(TerrainGrid terrain, Random rng)
    {
        int treeSize = Tweaks.LevelGen.TreeSize;

        var points = new Position[treeSize];
        for (int i = 0; i < treeSize; i++)
            points[i] = GeneratorUtils.GenerateInside(terrain.Size, Tweaks.LevelGen.BorderWidth, rng);

        var spawns = new List<Position> { points[0] };
        for (int i = 1; i < treeSize && spawns.Count < Tweaks.LevelGen.MaxPlayers; i++)
        {
            bool tooClose = false;
            foreach (var s in spawns)
            {
                if (Position.DistanceSquared(points[i], s) < Tweaks.LevelGen.MinSpawnDistanceSq)
                { tooClose = true; break; }
            }
            if (!tooClose) spawns.Add(points[i]);
        }

        // Kruskal's MST: each node starts in its own component
        int[] componentId = new int[treeSize];
        for (int i = 0; i < treeSize; i++) componentId[i] = i;

        var edges = new List<(int dist, int a, int b)>();
        for (int i = 0; i < treeSize; i++)
            for (int j = i + 1; j < treeSize; j++)
                edges.Add((Position.DistanceSquared(points[i], points[j]), i, j));
        edges.Sort((a, b) => a.dist.CompareTo(b.dist));

        int edgeCount = 0;
        foreach (var (_, a, b) in edges)
        {
            if (edgeCount >= treeSize - 1) break;
            int aComp = componentId[a], bComp = componentId[b];
            if (aComp == bComp) continue;
            edgeCount++;
            for (int k = 0; k < treeSize; k++)
                if (componentId[k] == bComp) componentId[k] = aComp;
            GeneratorUtils.DrawLine(terrain, points[a], points[b], TerrainPixel.LevelGenDirt);
        }

        return spawns.ToArray();
    }

    #region Deterministic (single-threaded)

    private static void RandomlyExpand(TerrainGrid terrain, Random rng)
    {
        int w = terrain.Width, h = terrain.Height;
        var queue = new Queue<Position>();

        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                if (terrain.GetPixelRaw(new Position(x, y)) != TerrainPixel.LevelGenDirt && terrain.HasLevelGenNeighbor(x, y))
                {
                    terrain.SetPixelRaw(new Position(x, y), TerrainPixel.LevelGenMark);
                    queue.Enqueue(new Position(x, y));
                }
            }

        int goal = Tweaks.LevelGen.TargetDirtAmount(terrain.Size);
        int generated = 0;
        int maxOdds = Tweaks.LevelGen.MaxDirtSpawnOdds;
        int progression = Tweaks.LevelGen.DirtSpawnProgression;

        while (queue.Count > 0 && generated < goal)
        {
            int total = queue.Count;
            for (int i = 0; i < total && generated < goal; i++)
            {
                var pos = queue.Dequeue();
                int xodds = maxOdds * Math.Min(w - pos.X, pos.X) / progression;
                int yodds = maxOdds * Math.Min(h - pos.Y, pos.Y) / progression;
                int odds = Math.Min(Math.Min(xodds, yodds), maxOdds);

                if (rng.Next(1000) < odds)
                {
                    if (terrain.GetPixelRaw(pos) != TerrainPixel.LevelGenDirt)
                    {
                        terrain.SetPixelRaw(pos, TerrainPixel.LevelGenDirt);
                        generated++;
                    }

                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = pos.X + dx, ny = pos.Y + dy;
                            if (GeneratorUtils.IsInterior(nx, ny, w, h))
                            {
                                var np = new Position(nx, ny);
                                if (terrain.GetPixelRaw(np) == TerrainPixel.LevelGenRock)
                                    queue.Enqueue(np);
                            }
                        }
                }
                else
                {
                    queue.Enqueue(pos);
                }
            }
        }

        ExpandCleanup(terrain);
    }

    private static void SmoothCavern(TerrainGrid terrain)
    {
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenDirt);

        int remaining = Tweaks.LevelGen.SmoothingSteps;
        while (SmoothOnce(terrain) > 0)
        {
            if (remaining >= 0 && --remaining <= 0) break;
        }

        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenRock);
    }

    private static int SmoothOnce(TerrainGrid terrain)
    {
        int w = terrain.Width, h = terrain.Height;
        var writes = new List<(int offset, TerrainPixel value)>();
        int changed = 0;

        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                var pos = new Position(x, y);
                var old = terrain.GetPixelRaw(pos);
                var newVal = GeneratorUtils.SmoothPixel(old, terrain.CountLevelGenNeighbors(pos));
                if (newVal != old)
                {
                    writes.Add((x + y * w, newVal));
                    changed++;
                }
            }

        foreach (var (offset, value) in writes)
            terrain.SetPixelRaw(offset, value);

        return changed;
    }

    #endregion

    #region Optimized (parallel, non-deterministic)

    private static void GenerateOptimized(TerrainGrid terrain, Position[] spawns)
    {
        int w = terrain.Width, h = terrain.Height;

        // 1. Widen MST skeleton so CA smoothing can't erode thin paths
        DilatePaths(terrain, radius: 3);

        // 2. Distance transform from path pixels (Manhattan, two-pass)
        var dist = DistanceFromDirt(terrain, w, h);

        // 3. Two-octave noise grids for spatially coherent rock formations.
        //    Small noise: local texture (~11px cells on 320x200).
        //    Large noise: map-spanning rock formations (~40px cells on 320x200).
        var gridRng = new Random();
        int smallCell = Math.Max(8, Math.Min(w, h) / 18);
        float[] smallNoise = BuildNoiseGrid(w, h, smallCell, gridRng);
        int smallGw = (w + smallCell - 1) / smallCell + 2;

        int largeCell = Math.Max(16, Math.Min(w, h) / 4);
        float[] largeNoise = BuildNoiseGrid(w, h, largeCell, gridRng);
        int largeGw = (w + largeCell - 1) / largeCell + 2;

        // 4. Stochastic fill: combined noise controls rock/dirt tendency per region.
        //    Large noise creates broad formations; small noise adds irregular edges.
        //    Distance boost keeps tunnels near MST paths clear.
        int progression = Tweaks.LevelGen.DirtSpawnProgression;
        float decayRadius = Math.Min(w, h) * 0.12f;
        float invSmall = 1f / smallCell;
        float invLarge = 1f / largeCell;

        Parallel.For(1, h - 1, y =>
        {
            var rng = new Random();
            for (int x = 1; x < w - 1; x++)
            {
                int offset = x + y * w;
                if (terrain.GetPixelRaw(offset) == TerrainPixel.LevelGenDirt)
                    continue;

                float sn = SampleNoise(smallNoise, smallGw, x, y, invSmall);
                float ln = SampleNoise(largeNoise, largeGw, x, y, invLarge);
                float combined = ln * 0.60f + sn * 0.40f;

                float d = dist[offset];
                float distBoost = 1f / (1f + d * d / (decayRadius * decayRadius));
                float edgeX = MathF.Min(w - x, x) / (float)progression;
                float edgeY = MathF.Min(h - y, y) / (float)progression;
                float edgeFactor = MathF.Min(MathF.Min(edgeX, edgeY), 1f);
                float prob = (0.15f + combined * 0.50f + distBoost * 0.40f) * edgeFactor;

                if (rng.NextSingle() < prob)
                    terrain.SetPixelRaw(offset, TerrainPixel.LevelGenDirt);
            }
        });

        // 5. Fixed-count parallel CA smoothing (7 passes)
        SmoothParallel(terrain, passes: 7);

        // 6. Guarantee spawn connectivity (carve tunnels if CA closed any)
        EnsureConnectivity(terrain, spawns);

        ExpandCleanup(terrain);
    }

    private static void DilatePaths(TerrainGrid terrain, int radius)
    {
        int w = terrain.Width, h = terrain.Height;
        int area = w * h;

        var sources = new List<int>(area / 10);
        for (int i = 0; i < area; i++)
            if (terrain.GetPixelRaw(i) == TerrainPixel.LevelGenDirt)
                sources.Add(i);

        foreach (int i in sources)
        {
            int cx = i % w, cy = i / w;
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (GeneratorUtils.IsInterior(nx, ny, w, h))
                        terrain.SetPixelRaw(nx + ny * w, TerrainPixel.LevelGenDirt);
                }
        }
    }

    private static int[] DistanceFromDirt(TerrainGrid terrain, int w, int h)
    {
        int area = w * h;
        var dist = new int[area];
        const int INF = int.MaxValue / 2;

        for (int i = 0; i < area; i++)
            dist[i] = terrain.GetPixelRaw(i) == TerrainPixel.LevelGenDirt ? 0 : INF;

        for (int y = 1; y < h; y++)
            for (int x = 1; x < w; x++)
            {
                int i = x + y * w;
                int up = dist[i - w] + 1;
                int left = dist[i - 1] + 1;
                int min = dist[i];
                if (up < min) min = up;
                if (left < min) min = left;
                dist[i] = min;
            }

        for (int y = h - 2; y >= 0; y--)
            for (int x = w - 2; x >= 0; x--)
            {
                int i = x + y * w;
                int down = dist[i + w] + 1;
                int right = dist[i + 1] + 1;
                int min = dist[i];
                if (down < min) min = down;
                if (right < min) min = right;
                dist[i] = min;
            }

        return dist;
    }

    private static void EnsureConnectivity(TerrainGrid terrain, Position[] spawns)
    {
        if (spawns.Length < 2) return;

        int w = terrain.Width;
        var visited = new bool[w * terrain.Height];
        var queue = new Queue<int>();

        // Flood from first spawn to find all reachable dirt
        GeneratorUtils.FloodFillDirt(terrain, visited, queue, spawns[0].X + spawns[0].Y * w);

        // Connect any unreachable spawns by carving thick tunnels back to spawn[0]
        for (int i = 1; i < spawns.Length; i++)
        {
            int si = spawns[i].X + spawns[i].Y * w;
            if (!visited[si])
            {
                GeneratorUtils.DrawThickLine(terrain, spawns[i], spawns[0], TerrainPixel.LevelGenDirt, radius: 3);
                queue.Clear();
                GeneratorUtils.FloodFillDirt(terrain, visited, queue, si);
            }
        }
    }

    private static void SmoothParallel(TerrainGrid terrain, int passes)
    {
        int w = terrain.Width, h = terrain.Height;

        var stagedWrites = new ThreadLocal<List<(int offset, TerrainPixel value)>>(
            () => new List<(int, TerrainPixel)>(256), trackAllValues: true);

        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenDirt);

        for (int pass = 0; pass < passes; pass++)
        {
            Parallel.For(1, h - 1, y =>
            {
                var writes = stagedWrites.Value!;
                for (int x = 1; x < w - 1; x++)
                {
                    var pos = new Position(x, y);
                    var old = terrain.GetPixelRaw(pos);
                    var newVal = GeneratorUtils.SmoothPixel(old, terrain.CountLevelGenNeighbors(pos));
                    if (newVal != old)
                        writes.Add((x + y * w, newVal));
                }
            });

            foreach (var writes in stagedWrites.Values)
            {
                foreach (var (offset, value) in writes)
                    terrain.SetPixelRaw(offset, value);
                writes.Clear();
            }
        }

        stagedWrites.Dispose();
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenRock);
    }

    private static float[] BuildNoiseGrid(int w, int h, int cellSize, Random rng)
    {
        int gw = (w + cellSize - 1) / cellSize + 2;
        int gh = (h + cellSize - 1) / cellSize + 2;
        float[] grid = new float[gw * gh];
        for (int i = 0; i < grid.Length; i++)
            grid[i] = rng.NextSingle();
        return grid;
    }

    private static float SampleNoise(float[] grid, int gw, int x, int y, float invCell)
    {
        float fx = x * invCell, fy = y * invCell;
        int ix = (int)fx, iy = (int)fy;
        float tx = fx - ix, ty = fy - iy;
        return grid[ix + iy * gw] * (1 - tx) * (1 - ty)
             + grid[ix + 1 + iy * gw] * tx * (1 - ty)
             + grid[ix + (iy + 1) * gw] * (1 - tx) * ty
             + grid[ix + 1 + (iy + 1) * gw] * tx * ty;
    }

    #endregion

    private static void ExpandCleanup(TerrainGrid terrain)
    {
        for (int i = 0; i < terrain.Size.Area; i++)
        {
            if (terrain[i] == TerrainPixel.LevelGenMark)
                terrain[i] = TerrainPixel.LevelGenRock;
        }
    }
}
