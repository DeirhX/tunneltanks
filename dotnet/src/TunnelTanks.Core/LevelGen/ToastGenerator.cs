namespace TunnelTanks.Core.LevelGen;

using System.Collections.Concurrent;
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
    public (Terrain terrain, Position[] spawns) Generate(Size size, int? seed = null,
        LevelGenMode mode = LevelGenMode.Deterministic)
    {
        var terrain = new Terrain(size);
        GeneratorUtils.FillAll(terrain, TerrainPixel.LevelGenRock);

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var spawns = GenerateTree(terrain, rng);

        if (mode == LevelGenMode.Optimized)
        {
            RandomlyExpandParallel(terrain, rng);
            SmoothCavernParallel(terrain);
        }
        else
        {
            RandomlyExpand(terrain, rng);
            SmoothCavern(terrain);
        }

        return (terrain, spawns);
    }

    private Position[] GenerateTree(Terrain terrain, Random rng)
    {
        const int treeSize = 150;
        const int borderWidth = 30;
        const int maxPlayers = 2;
        const int minDistSq = 150 * 150;

        var points = new Position[treeSize];
        for (int i = 0; i < treeSize; i++)
            points[i] = GeneratorUtils.GenerateInside(terrain.Size, borderWidth, rng);

        var spawns = new List<Position> { points[0] };
        for (int i = 1; i < treeSize && spawns.Count < maxPlayers; i++)
        {
            bool tooClose = false;
            foreach (var s in spawns)
            {
                if (GeneratorUtils.PointDistanceSquared(points[i], s) < minDistSq)
                { tooClose = true; break; }
            }
            if (!tooClose) spawns.Add(points[i]);
        }

        int[] dsets = new int[treeSize];
        for (int i = 0; i < treeSize; i++) dsets[i] = i;

        var pairs = new List<(int dist, int a, int b)>();
        for (int i = 0; i < treeSize; i++)
            for (int j = i + 1; j < treeSize; j++)
                pairs.Add((GeneratorUtils.PointDistanceSquared(points[i], points[j]), i, j));
        pairs.Sort((a, b) => a.dist.CompareTo(b.dist));

        int edges = 0;
        foreach (var (_, a, b) in pairs)
        {
            if (edges >= treeSize - 1) break;
            int aset = dsets[a], bset = dsets[b];
            if (aset == bset) continue;
            edges++;
            for (int k = 0; k < treeSize; k++)
                if (dsets[k] == bset) dsets[k] = aset;
            GeneratorUtils.DrawLine(terrain, points[a], points[b], TerrainPixel.LevelGenDirt);
        }

        return spawns.ToArray();
    }

    #region Deterministic (single-threaded)

    private void RandomlyExpand(Terrain terrain, Random rng)
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
                            if (nx > 0 && nx < w - 1 && ny > 0 && ny < h - 1)
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

    private void SmoothCavern(Terrain terrain)
    {
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenDirt);
        int steps = Tweaks.LevelGen.SmoothingSteps;
        if (steps < 0)
            while (SmoothOnce(terrain) > 0) { }
        else
            while (SmoothOnce(terrain) > 0 && --steps > 0) { }
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenRock);
    }

    private int SmoothOnce(Terrain terrain)
    {
        int w = terrain.Width, h = terrain.Height;
        var writes = new List<(int offset, TerrainPixel value)>();
        int changed = 0;

        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                var pos = new Position(x, y);
                var old = terrain.GetPixelRaw(pos);
                int n = terrain.CountLevelGenNeighbors(pos);
                bool paintRock = (old != TerrainPixel.LevelGenDirt) ? (n >= 3) : (n > 4);
                var newVal = paintRock ? TerrainPixel.LevelGenRock : TerrainPixel.LevelGenDirt;
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

    private void RandomlyExpandParallel(Terrain terrain, Random seedRng)
    {
        int w = terrain.Width, h = terrain.Height;

        // Phase 1: Find initial frontier (parallelize scan)
        var initBag = new ConcurrentBag<Position>();
        Parallel.For(1, h - 1, y =>
        {
            for (int x = 1; x < w - 1; x++)
            {
                if (terrain.GetPixelRaw(new Position(x, y)) != TerrainPixel.LevelGenDirt
                    && terrain.HasLevelGenNeighbor(x, y))
                {
                    terrain.SetPixelRaw(new Position(x, y), TerrainPixel.LevelGenMark);
                    initBag.Add(new Position(x, y));
                }
            }
        });

        // Phase 2: Split frontier across worker queues (like C++)
        int workerCount = Tweaks.Perf.ParallelismDegree;
        var workerQueues = new Queue<Position>[workerCount];
        for (int i = 0; i < workerCount; i++)
            workerQueues[i] = new Queue<Position>();

        int idx = 0;
        foreach (var pos in initBag)
            workerQueues[idx++ % workerCount].Enqueue(pos);

        // Phase 3: Parallel expansion — each worker has its own queue and RNG.
        // Workers read the shared terrain for neighbor checks but only write
        // LevelGenDirt (idempotent), so benign races are acceptable.
        int goal = Tweaks.LevelGen.TargetDirtAmount(terrain.Size);
        int generated = 0;
        int maxOdds = Tweaks.LevelGen.MaxDirtSpawnOdds;
        int progression = Tweaks.LevelGen.DirtSpawnProgression;

        Parallel.For(0, workerCount, workerId =>
        {
            var q = workerQueues[workerId];
            var rng = new Random(seedRng.Next() ^ (workerId + 1));
            int localGen = 0;

            while (q.Count > 0 && Volatile.Read(ref generated) < goal)
            {
                int total = q.Count;
                for (int i = 0; i < total && Volatile.Read(ref generated) < goal; i++)
                {
                    var pos = q.Dequeue();
                    int xodds = maxOdds * Math.Min(w - pos.X, pos.X) / progression;
                    int yodds = maxOdds * Math.Min(h - pos.Y, pos.Y) / progression;
                    int odds = Math.Min(Math.Min(xodds, yodds), maxOdds);

                    if (rng.Next(1000) < odds)
                    {
                        if (terrain.GetPixelRaw(pos) != TerrainPixel.LevelGenDirt)
                        {
                            terrain.SetPixelRaw(pos, TerrainPixel.LevelGenDirt);
                            localGen++;
                            if (localGen >= 64)
                            {
                                Interlocked.Add(ref generated, localGen);
                                localGen = 0;
                            }
                        }

                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = pos.X + dx, ny = pos.Y + dy;
                                if (nx > 0 && nx < w - 1 && ny > 0 && ny < h - 1)
                                {
                                    if (terrain.GetPixelRaw(new Position(nx, ny)) == TerrainPixel.LevelGenRock)
                                        q.Enqueue(new Position(nx, ny));
                                }
                            }
                    }
                    else
                    {
                        q.Enqueue(pos);
                    }
                }
            }
            Interlocked.Add(ref generated, localGen);
        });

        ExpandCleanup(terrain);
    }

    private void SmoothCavernParallel(Terrain terrain)
    {
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenDirt);
        int steps = Tweaks.LevelGen.SmoothingSteps;
        if (steps < 0)
            while (SmoothOnceParallel(terrain) > 0) { }
        else
            while (SmoothOnceParallel(terrain) > 0 && --steps > 0) { }
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenRock);
    }

    private int SmoothOnceParallel(Terrain terrain)
    {
        int w = terrain.Width, h = terrain.Height;
        int changed = 0;

        var stagedWrites = new ThreadLocal<List<(int offset, TerrainPixel value)>>(
            () => new List<(int, TerrainPixel)>(256), trackAllValues: true);

        Parallel.For(1, h - 1, y =>
        {
            var writes = stagedWrites.Value!;
            for (int x = 1; x < w - 1; x++)
            {
                var pos = new Position(x, y);
                var old = terrain.GetPixelRaw(pos);
                int n = terrain.CountLevelGenNeighbors(pos);
                bool paintRock = (old != TerrainPixel.LevelGenDirt) ? (n >= 3) : (n > 4);
                var newVal = paintRock ? TerrainPixel.LevelGenRock : TerrainPixel.LevelGenDirt;
                if (newVal != old)
                    writes.Add((x + y * w, newVal));
            }
        });

        foreach (var writes in stagedWrites.Values)
        {
            changed += writes.Count;
            foreach (var (offset, value) in writes)
                terrain.SetPixelRaw(offset, value);
            writes.Clear();
        }

        stagedWrites.Dispose();
        return changed;
    }

    #endregion

    private static void ExpandCleanup(Terrain terrain)
    {
        for (int i = 0; i < terrain.Size.Area; i++)
        {
            if (terrain[i] == TerrainPixel.LevelGenMark || terrain[i] == TerrainPixel.LevelGenRock)
                terrain[i] = TerrainPixel.LevelGenRock;
        }
    }
}
