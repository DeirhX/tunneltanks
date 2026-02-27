namespace Tunnerer.Core.Terrain;

using Tunnerer.Core.Types;

public class TerrainGrid
{
    private readonly TerrainPixel[] _data;
    private readonly int[] _neighborOffsets;
    private readonly List<Position> _changeList = new();

    public Size Size { get; }
    public int Width => Size.X;
    public int Height => Size.Y;

    public TerrainGrid(Size size)
    {
        Size = size;
        _data = new TerrainPixel[size.Area];
        _neighborOffsets = BuildNeighborOffsets(size.X);
    }

    private static int[] BuildNeighborOffsets(int w)
    {
        var offsets = new int[8];
        int i = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                offsets[i++] = dx + dy * w;
            }
        return offsets;
    }

    public TerrainPixel this[int offset]
    {
        get => _data[offset];
        set => _data[offset] = value;
    }

    public TerrainPixel this[int x, int y]
    {
        get => _data[x + y * Width];
        set => _data[x + y * Width] = value;
    }

    public TerrainPixel GetPixel(Position pos)
    {
        if (!IsInside(pos)) return TerrainPixel.Rock;
        return _data[pos.X + pos.Y * Width];
    }

    public void SetPixelRaw(Position pos, TerrainPixel value) => _data[pos.X + pos.Y * Width] = value;
    public void SetPixelRaw(int offset, TerrainPixel value) => _data[offset] = value;
    public TerrainPixel GetPixelRaw(Position pos) => _data[pos.X + pos.Y * Width];
    public TerrainPixel GetPixelRaw(int offset) => _data[offset];

    public void SetPixel(Position pos, TerrainPixel value)
    {
        _data[pos.X + pos.Y * Width] = value;
        CommitPixel(pos);
    }

    public void CommitPixel(Position pos) => _changeList.Add(pos);

    public bool IsInside(Position pos) => Size.FitsInside(pos);

    public int CountDirtNeighbors(Position pos)
    {
        int offset = pos.X + pos.Y * Width;
        int count = 0;
        for (int i = 0; i < 8; i++)
            if (Pixel.IsDirt(_data[offset + _neighborOffsets[i]])) count++;
        return count;
    }

    /// <summary>
    /// Sums the byte values of 8 neighbors. During level generation, LevelGenDirt=0 and
    /// LevelGenRock=1, so the result equals the number of rock neighbors (0..8).
    /// </summary>
    public int CountLevelGenNeighbors(Position pos)
    {
        int offset = pos.X + pos.Y * Width;
        int sum = 0;
        for (int i = 0; i < 8; i++)
            sum += (byte)_data[offset + _neighborOffsets[i]];
        return sum;
    }

    public bool HasLevelGenNeighbor(int x, int y)
    {
        int offset = x + y * Width;
        for (int i = 0; i < 8; i++)
            if (_data[offset + _neighborOffsets[i]] == TerrainPixel.LevelGenDirt) return true;
        return false;
    }

    public IReadOnlyList<Position> GetChangeList() => _changeList;
    public void ClearChangeList() => _changeList.Clear();

    public void DrawChangesToSurface(uint[] surface)
    {
        foreach (var pos in _changeList)
        {
            var color = Pixel.GetColor(GetPixel(pos));
            surface[pos.X + pos.Y * Width] = color.ToArgb();
        }
        _changeList.Clear();
    }

    public void DrawAllToSurface(uint[] surface)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var color = Pixel.GetColor(_data[x + y * Width]);
                surface[x + y * Width] = color.ToArgb();
            }
    }

    public void MaterializeTerrain(int? seed = null, bool parallel = false)
    {
        if (parallel)
        {
            MaterializeTerrainParallel(seed);
            return;
        }

        var rng = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        for (int i = 0; i < _data.Length; i++)
            _data[i] = MaterializePixel(_data[i], rng);
    }

    private void MaterializeTerrainParallel(int? seed)
    {
        const int chunkSize = 4096;
        int length = _data.Length;
        int chunks = (length + chunkSize - 1) / chunkSize;
        int baseSeed = seed ?? Environment.TickCount;

        Parallel.For(0, chunks, chunk =>
        {
            var rng = new Random(baseSeed + chunk);
            int from = chunk * chunkSize;
            int to = Math.Min(from + chunkSize, length);
            for (int i = from; i < to; i++)
                _data[i] = MaterializePixel(_data[i], rng);
        });
    }

    private static TerrainPixel MaterializePixel(TerrainPixel p, Random rng) => p switch
    {
        TerrainPixel.LevelGenDirt => rng.Next(2) == 0 ? TerrainPixel.DirtHigh : TerrainPixel.DirtLow,
        TerrainPixel.LevelGenRock => TerrainPixel.Rock,
        TerrainPixel.LevelGenMark => TerrainPixel.Rock,
        _ => p,
    };

    public void DecorateTerrain(int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        int w = Width, h = Height;

        PlaceEnergyVeins(rng, w, h);
        PlaceConcreteRuins(rng, w, h);
        CleanupIsolatedPixels(w, h);
    }

    /// <summary>
    /// Removes single-pixel anomalies that cause rendering artifacts.
    /// An isolated pixel is one whose solid/empty state differs from 6+ of its 8 neighbors.
    /// </summary>
    private void CleanupIsolatedPixels(int w, int h)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                int row = y * w;
                for (int x = 1; x < w - 1; x++)
                {
                    int offset = row + x;
                    var p = _data[offset];
                    bool isSolid = IsSolidForCleanup(p);

                    int solidNeighbors = 0;
                    TerrainPixel dominantSolid = TerrainPixel.Rock;
                    TerrainPixel dominantEmpty = TerrainPixel.Blank;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nRow = (y + dy) * w;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var np = _data[nRow + x + dx];
                            if (IsSolidForCleanup(np))
                            {
                                solidNeighbors++;
                                dominantSolid = np;
                            }
                            else
                            {
                                dominantEmpty = np;
                            }
                        }
                    }

                    if (isSolid && solidNeighbors <= 1)
                        _data[offset] = dominantEmpty;
                    else if (!isSolid && solidNeighbors >= 7)
                        _data[offset] = dominantSolid;
                }
            }
        }
    }

    private static bool IsSolidForCleanup(TerrainPixel p)
    {
        if (p == TerrainPixel.Blank) return false;
        if (p == TerrainPixel.DirtGrow) return false;
        return true;
    }

    private static bool IsCaveFloor(TerrainPixel p) =>
        p == TerrainPixel.Blank || Pixel.IsDirt(p) || p == TerrainPixel.DirtGrow;

    private void PlaceEnergyVeins(Random rng, int w, int h)
    {
        var rockBoundary = new List<int>();
        for (int y = 3; y < h - 3; y++)
        {
            int row = y * w;
            for (int x = 3; x < w - 3; x++)
            {
                int offset = row + x;
                if (_data[offset] != TerrainPixel.Rock) continue;
                bool nearCave = false;
                for (int dy = -2; dy <= 2 && !nearCave; dy++)
                    for (int dx = -2; dx <= 2 && !nearCave; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((uint)nx < (uint)w && (uint)ny < (uint)h && IsCaveFloor(_data[nx + ny * w]))
                            nearCave = true;
                    }
                if (nearCave) rockBoundary.Add(offset);
            }
        }

        int veinCount = Math.Max(6, rockBoundary.Count / 40);
        for (int v = 0; v < veinCount && rockBoundary.Count > 0; v++)
        {
            int pick = rng.Next(rockBoundary.Count);
            int co = rockBoundary[pick];
            int vx = co % w, vy = co / w;

            // Thin snaking vein: single-cell wide, longer walk
            int veinLen = rng.Next(5, 16);
            int dirX = rng.Next(-1, 2), dirY = rng.Next(-1, 2);
            if (dirX == 0 && dirY == 0) dirX = 1;

            for (int step = 0; step < veinLen; step++)
            {
                if (vx < 2 || vx >= w - 2 || vy < 2 || vy >= h - 2) break;

                int offset = vx + vy * w;
                if (_data[offset] == TerrainPixel.Rock)
                {
                    _data[offset] = step < veinLen / 3 ? TerrainPixel.EnergyLow
                        : step < veinLen * 2 / 3 ? TerrainPixel.EnergyMedium
                        : TerrainPixel.EnergyHigh;
                }
                else if (!Pixel.IsEnergy(_data[offset]))
                    break;

                // Continue along direction with slight random wobble
                vx += dirX;
                vy += dirY;
                if (rng.Next(3) == 0) dirX = Math.Clamp(dirX + rng.Next(-1, 2), -1, 1);
                if (rng.Next(3) == 0) dirY = Math.Clamp(dirY + rng.Next(-1, 2), -1, 1);
                if (dirX == 0 && dirY == 0) dirX = rng.Next(2) * 2 - 1;
            }
        }
    }

    private void PlaceConcreteRuins(Random rng, int w, int h)
    {
        int ruinCount = (w * h) / 16000;

        for (int r = 0; r < ruinCount; r++)
        {
            int cx = rng.Next(6, w - 6);
            int cy = rng.Next(6, h - 6);

            if (!IsCaveFloor(_data[cx + cy * w])) continue;

            // Short wall fragment
            int len = rng.Next(2, 5);
            bool horizontal = rng.Next(2) == 0;
            for (int i = 0; i < len; i++)
            {
                int nx = horizontal ? cx + i : cx;
                int ny = horizontal ? cy : cy + i;
                if (nx < 1 || nx >= w - 1 || ny < 1 || ny >= h - 1) continue;
                int offset = nx + ny * w;
                if (IsCaveFloor(_data[offset]))
                    _data[offset] = rng.Next(2) == 0 ? TerrainPixel.ConcreteHigh : TerrainPixel.ConcreteLow;
            }
        }
    }

    public void Fill(TerrainPixel value) => Array.Fill(_data, value);

    public void DrawAllToSurfaceParallel(uint[] surface)
    {
        int w = Width, h = Height;
        Parallel.For(0, h, y =>
        {
            int rowOffset = y * w;
            for (int x = 0; x < w; x++)
            {
                var color = Pixel.GetColor(_data[rowOffset + x]);
                surface[rowOffset + x] = color.ToArgb();
            }
        });
    }

    public ReadOnlySpan<TerrainPixel> Data => _data;
}
