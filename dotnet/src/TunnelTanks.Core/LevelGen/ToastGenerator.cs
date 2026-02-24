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
    public (Terrain terrain, Position[] spawns) Generate(Size size, int? seed = null,
        LevelGenMode mode = LevelGenMode.Deterministic)
    {
        var terrain = new Terrain(size);
        GeneratorUtils.FillAll(terrain, TerrainPixel.LevelGenRock);

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

    private void GenerateOptimized(Terrain terrain, Position[] spawns)
    {
        int w = terrain.Width, h = terrain.Height;

        // 1. Widen MST skeleton so CA smoothing can't erode thin paths
        DilatePaths(terrain, radius: 3);

        // 2. Distance transform from path pixels (Manhattan, two-pass)
        var dist = DistanceFromDirt(terrain, w, h);

        // 3. Build a low-frequency noise grid for spatially coherent rock formations.
        //    Each cell covers ~cellSize pixels; bilinear interpolation smooths between cells.
        int cellSize = Math.Max(8, Math.Min(w, h) / 18);
        int gw = (w + cellSize - 1) / cellSize + 2;
        int gh = (h + cellSize - 1) / cellSize + 2;
        float[] noiseGrid = new float[gw * gh];
        var gridRng = new Random();
        for (int i = 0; i < noiseGrid.Length; i++)
            noiseGrid[i] = gridRng.NextSingle();

        // 4. Stochastic fill: spatially coherent noise + distance boost near paths.
        //    The coherent noise creates large rock/dirt zones that CA shapes into partitions.
        //    The distance boost ensures tunnel corridors near MST paths survive.
        int progression = Tweaks.LevelGen.DirtSpawnProgression;
        float decayRadius = Math.Min(w, h) * 0.12f;
        float invCell = 1f / cellSize;

        Parallel.For(1, h - 1, y =>
        {
            var rng = new Random();
            float fy = y * invCell;
            int iy = (int)fy;
            float ty = fy - iy;

            for (int x = 1; x < w - 1; x++)
            {
                int offset = x + y * w;
                if (terrain.GetPixelRaw(offset) == TerrainPixel.LevelGenDirt)
                    continue;

                // Bilinear interpolation of noise grid
                float fx = x * invCell;
                int ix = (int)fx;
                float tx = fx - ix;
                float n00 = noiseGrid[ix + iy * gw];
                float n10 = noiseGrid[ix + 1 + iy * gw];
                float n01 = noiseGrid[ix + (iy + 1) * gw];
                float n11 = noiseGrid[ix + 1 + (iy + 1) * gw];
                float noise = n00 * (1 - tx) * (1 - ty) + n10 * tx * (1 - ty)
                            + n01 * (1 - tx) * ty + n11 * tx * ty;

                float d = dist[offset];
                float distBoost = 1f / (1f + d * d / (decayRadius * decayRadius));
                float edgeX = MathF.Min(w - x, x) / (float)progression;
                float edgeY = MathF.Min(h - y, y) / (float)progression;
                float edgeFactor = MathF.Min(MathF.Min(edgeX, edgeY), 1f);
                float prob = (0.30f + noise * 0.40f + distBoost * 0.35f) * edgeFactor;

                if (rng.NextSingle() < prob)
                    terrain.SetPixelRaw(offset, TerrainPixel.LevelGenDirt);
            }
        });

        // 4. Fixed-count parallel CA smoothing (7 passes)
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenDirt);
        for (int i = 0; i < 7; i++)
            SmoothOnceParallel(terrain);
        GeneratorUtils.SetOutside(terrain, TerrainPixel.LevelGenRock);

        // 5. Guarantee spawn connectivity (carve tunnels if CA closed any)
        EnsureConnectivity(terrain, spawns);

        ExpandCleanup(terrain);
    }

    private static void DilatePaths(Terrain terrain, int radius)
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
                    if (nx > 0 && nx < w - 1 && ny > 0 && ny < h - 1)
                        terrain.SetPixelRaw(nx + ny * w, TerrainPixel.LevelGenDirt);
                }
        }
    }

    private static int[] DistanceFromDirt(Terrain terrain, int w, int h)
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

    private static void EnsureConnectivity(Terrain terrain, Position[] spawns)
    {
        if (spawns.Length < 2) return;

        int w = terrain.Width, h = terrain.Height;
        var visited = new bool[w * h];
        var queue = new Queue<int>();

        int start = spawns[0].X + spawns[0].Y * w;
        visited[start] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int cx = idx % w, cy = idx / w;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx <= 0 || nx >= w - 1 || ny <= 0 || ny >= h - 1) continue;
                    int ni = nx + ny * w;
                    if (visited[ni]) continue;
                    if (terrain.GetPixelRaw(ni) != TerrainPixel.LevelGenDirt) continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
        }

        for (int i = 1; i < spawns.Length; i++)
        {
            int si = spawns[i].X + spawns[i].Y * w;
            if (!visited[si])
            {
                GeneratorUtils.DrawThickLine(terrain, spawns[i], spawns[0], TerrainPixel.LevelGenDirt, radius: 3);

                queue.Clear();
                int s = spawns[i].X + spawns[i].Y * w;
                if (!visited[s]) { visited[s] = true; queue.Enqueue(s); }
                while (queue.Count > 0)
                {
                    int idx = queue.Dequeue();
                    int cx = idx % w, cy = idx / w;
                    for (int dy2 = -1; dy2 <= 1; dy2++)
                        for (int dx2 = -1; dx2 <= 1; dx2++)
                        {
                            if (dx2 == 0 && dy2 == 0) continue;
                            int nx = cx + dx2, ny = cy + dy2;
                            if (nx <= 0 || nx >= w - 1 || ny <= 0 || ny >= h - 1) continue;
                            int ni = nx + ny * w;
                            if (visited[ni]) continue;
                            if (terrain.GetPixelRaw(ni) != TerrainPixel.LevelGenDirt) continue;
                            visited[ni] = true;
                            queue.Enqueue(ni);
                        }
                }
            }
        }
    }

    private void SmoothOnceParallel(Terrain terrain)
    {
        int w = terrain.Width, h = terrain.Height;

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
            foreach (var (offset, value) in writes)
                terrain.SetPixelRaw(offset, value);
            writes.Clear();
        }

        stagedWrites.Dispose();
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
