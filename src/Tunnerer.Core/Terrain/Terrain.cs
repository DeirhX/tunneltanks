namespace Tunnerer.Core.Terrain;

using Tunnerer.Core.Config;
using Tunnerer.Core.Thermal;
using Tunnerer.Core.Types;

public class TerrainGrid
{
    private readonly TerrainPixel[] _data;
    private readonly float[] _heatTemperature;
    private readonly TerrainHeatEngine _heatEngine = new();
    private readonly int[] _neighborOffsets;
    private readonly List<Position> _changeList = new();
    private bool _hasHeatDirtyRect;
    private int _heatDirtyMinX;
    private int _heatDirtyMinY;
    private int _heatDirtyMaxX;
    private int _heatDirtyMaxY;

    public Size Size { get; }
    public int Width => Size.X;
    public int Height => Size.Y;

    public TerrainGrid(Size size)
    {
        Size = size;
        _data = new TerrainPixel[size.Area];
        _heatTemperature = new float[size.Area];
        _neighborOffsets = BuildNeighborOffsets(size.X);
    }

    // ------------------------------------------------------------------
    //  Heat map: continuous temperature per pixel (float-authoritative simulation state).
    // ------------------------------------------------------------------

    public float GetHeatTemperature(int offset)
        => (uint)offset < (uint)_heatTemperature.Length ? _heatTemperature[offset] : 0f;

    public float GetHeatTemperature(Position pos)
    {
        int offset = pos.X + pos.Y * Width;
        if ((uint)offset >= (uint)_heatTemperature.Length)
            return 0f;
        return _heatEngine.GetTemperatureAt(_heatTemperature, _heatTemperature.Length, offset);
    }

    public void AddHeat(Position pos, int amount)
    {
        int offset = pos.X + pos.Y * Width;
        if ((uint)offset >= (uint)_heatTemperature.Length) return;
        float old = _heatTemperature[offset];
        _heatEngine.AddEnergyAt(_heatTemperature, offset, amount);
        float next = _heatTemperature[offset];
        if (MathF.Abs(next - old) > 0.0001f)
            MarkHeatDirty(pos.X, pos.Y);
        CommitPixel(pos);
    }

    public void AddHeatRadius(Position center, int amount, int radius)
    {
        int radiusSq = radius * radius;
        ForEachInRadius(center, radius, (nx, ny, dx, dy) =>
        {
            int dist2 = dx * dx + dy * dy;
            if (dist2 > radiusSq) return;
            float falloff = 1f - (float)dist2 / radiusSq;
            int scaled = (int)(amount * falloff);
            if (scaled == 0) return;
            int offset = nx + ny * Width;
            float old = _heatTemperature[offset];
            _heatEngine.AddEnergyAt(_heatTemperature, offset, scaled);
            float next = _heatTemperature[offset];
            if (MathF.Abs(next - old) > 0.0001f)
                MarkHeatDirty(nx, ny);
        });
    }

    private float[]? _heatTemp;

    public void CoolDown(int decayAmount, float diffuseRate = 0.12f)
    {
        int w = Width, h = Height;
        int len = w * h;

        if (Tweaks.World.EnableMaterialHeatExchange)
        {
            // In material-physics mode, avoid the legacy blur/mix pass because it can
            // create heat through per-cell rounding. Use only pairwise exchange and
            // ambient coupling so heat moves through explicit flux terms.
            if (_heatTemp == null || _heatTemp.Length < len)
                _heatTemp = new float[len];
            Array.Copy(_heatTemperature, _heatTemp, len);
            _heatEngine.Step(_heatTemperature, _data, w, h);
            for (int i = 0; i < len; i++)
            {
                if (MathF.Abs(_heatTemperature[i] - _heatTemp[i]) < 0.0001f) continue;
                int x = i % w;
                int y = i / w;
                MarkHeatDirty(x, y);
            }
            return;
        }

        if (_heatTemp == null || _heatTemp.Length < len)
            _heatTemp = new float[len];

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int idx = row + x;
                float center = _heatTemperature[idx];
                if (MathF.Abs(center) < 0.0001f && x > 0 && x < w - 1 && y > 0 && y < h - 1)
                {
                    float neighborSum = _heatTemperature[idx - 1] + _heatTemperature[idx + 1] +
                                        _heatTemperature[idx - w] + _heatTemperature[idx + w];
                    if (MathF.Abs(neighborSum) < 0.0001f) { _heatTemp[idx] = 0f; continue; }
                }

                float sum = center * 4f;
                int cnt = 4;
                if (x > 0) { sum += _heatTemperature[idx - 1]; cnt++; }
                if (x < w - 1) { sum += _heatTemperature[idx + 1]; cnt++; }
                if (y > 0) { sum += _heatTemperature[idx - w]; cnt++; }
                if (y < h - 1) { sum += _heatTemperature[idx + w]; cnt++; }

                float blurred = sum / cnt;
                float mixed = center + (blurred - center) * diffuseRate;
                float next = mixed - decayAmount;
                _heatTemp[idx] = next;
                if (MathF.Abs(next - _heatTemperature[idx]) > 0.0001f)
                    MarkHeatDirty(x, y);
            }
        }

        Array.Copy(_heatTemp, _heatTemperature, len);
    }

    public float SampleAverageHeat(Position center, int radius)
    {
        float sum = 0f;
        int count = 0;
        ForEachInRadius(center, radius, (nx, ny, _, _) =>
        {
            int idx = ny * Width + nx;
            sum += _heatEngine.GetTemperatureAt(_heatTemperature, _heatTemperature.Length, idx);
            count++;
        });
        return count > 0 ? sum / (count * 255f) : 0f;
    }

    public int CountCellsInRadiusArea(Position center, int radius)
    {
        int count = 0;
        ForEachInRadius(center, radius, (_, _, _, _) => count++);
        return count;
    }

    /// <summary>
    /// Applies an exact integer total heat delta across the same square area used by
    /// <see cref="SampleAverageHeat"/>. Returns the actually applied integer total.
    /// </summary>
    public int AddHeatTotalInRadiusArea(Position center, int radius, int totalAmount)
    {
        if (totalAmount == 0)
            return 0;
        int count = CountCellsInRadiusArea(center, radius);
        if (count <= 0)
            return 0;

        int baseDelta = totalAmount / count;
        int remainder = totalAmount % count;
        int remainderAbs = Math.Abs(remainder);
        int remainderSign = Math.Sign(remainder);

        int applied = 0;
        int i = 0;
        ForEachInRadius(center, radius, (nx, ny, _, _) =>
        {
            int delta = baseDelta;
            if (i < remainderAbs)
                delta += remainderSign;
            i++;

            if (delta == 0)
                return;

            int offset = nx + ny * Width;
            float old = _heatTemperature[offset];
            _heatEngine.AddEnergyAt(_heatTemperature, offset, delta);
            float next = _heatTemperature[offset];
            if (MathF.Abs(next - old) > 0.0001f)
                MarkHeatDirty(nx, ny);
            applied += (int)MathF.Round(next - old);
        });

        return applied;
    }

    private void ForEachInRadius(Position center, int radius, Action<int, int, int, int> visitor)
    {
        int w = Width, h = Height;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int ny = center.Y + dy;
            if ((uint)ny >= (uint)h) continue;
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = center.X + dx;
                if ((uint)nx >= (uint)w) continue;
                visitor(nx, ny, dx, dy);
            }
        }
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

    public bool TryGetHeatDirtyRect(out Rect dirtyRect)
    {
        if (_hasHeatDirtyRect)
        {
            dirtyRect = new Rect(
                _heatDirtyMinX,
                _heatDirtyMinY,
                _heatDirtyMaxX - _heatDirtyMinX + 1,
                _heatDirtyMaxY - _heatDirtyMinY + 1);
            return true;
        }

        dirtyRect = default;
        return false;
    }

    public void ClearHeatDirtyRect() => _hasHeatDirtyRect = false;

    private void MarkHeatDirty(int x, int y)
    {
        if (!_hasHeatDirtyRect)
        {
            _hasHeatDirtyRect = true;
            _heatDirtyMinX = _heatDirtyMaxX = x;
            _heatDirtyMinY = _heatDirtyMaxY = y;
            return;
        }

        if (x < _heatDirtyMinX) _heatDirtyMinX = x;
        if (x > _heatDirtyMaxX) _heatDirtyMaxX = x;
        if (y < _heatDirtyMinY) _heatDirtyMinY = y;
        if (y > _heatDirtyMaxY) _heatDirtyMaxY = y;
    }

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
        DrawAllToSurfaceInternal(surface, parallel: false);
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

        int veinCount = Math.Max(6, rockBoundary.Count / Tweaks.LevelGen.EnergyVeinCountDivisor);
        for (int v = 0; v < veinCount && rockBoundary.Count > 0; v++)
        {
            int pick = rng.Next(rockBoundary.Count);
            int co = rockBoundary[pick];
            int vx = co % w, vy = co / w;

            // Thin snaking vein: single-cell wide, longer walk
            int veinLen = rng.Next(Tweaks.LevelGen.EnergyVeinMinLength, Tweaks.LevelGen.EnergyVeinMaxLength);
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
        int ruinCount = (w * h) / Tweaks.LevelGen.RuinAreaPerRuin;

        for (int r = 0; r < ruinCount; r++)
        {
            int cx = rng.Next(6, w - 6);
            int cy = rng.Next(6, h - 6);

            if (!IsCaveFloor(_data[cx + cy * w])) continue;

            // Short wall fragment
            int len = rng.Next(Tweaks.LevelGen.RuinWallMinLength, Tweaks.LevelGen.RuinWallMaxLength);
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
        DrawAllToSurfaceInternal(surface, parallel: true);
    }

    private void DrawAllToSurfaceInternal(uint[] surface, bool parallel)
    {
        int w = Width, h = Height;
        if (parallel)
        {
            Parallel.For(0, h, y =>
            {
                int rowOffset = y * w;
                for (int x = 0; x < w; x++)
                {
                    var color = Pixel.GetColor(_data[rowOffset + x]);
                    surface[rowOffset + x] = color.ToArgb();
                }
            });
            return;
        }

        for (int y = 0; y < h; y++)
        {
            int rowOffset = y * w;
            for (int x = 0; x < w; x++)
            {
                var color = Pixel.GetColor(_data[rowOffset + x]);
                surface[rowOffset + x] = color.ToArgb();
            }
        }
    }

    public ReadOnlySpan<TerrainPixel> Data => _data;
}
