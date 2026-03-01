namespace Tunnerer.Core.Terrain;

using Tunnerer.Core.Config;
using Tunnerer.Core.Types;

public class TerrainGrid
{
    private readonly TerrainPixel[] _data;
    private readonly byte[] _heat;
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
        _heat = new byte[size.Area];
        _neighborOffsets = BuildNeighborOffsets(size.X);
    }

    // ------------------------------------------------------------------
    //  Heat map: continuous temperature per pixel (0=cold, 255=max)
    // ------------------------------------------------------------------

    public byte GetHeat(int offset) => _heat[offset];

    public byte GetHeat(Position pos)
    {
        int offset = pos.X + pos.Y * Width;
        return (uint)offset < (uint)_heat.Length ? _heat[offset] : (byte)0;
    }

    public void AddHeat(Position pos, int amount)
    {
        int offset = pos.X + pos.Y * Width;
        if ((uint)offset >= (uint)_heat.Length) return;
        byte old = _heat[offset];
        int val = old + amount;
        byte next = (byte)Math.Clamp(val, 0, 255);
        _heat[offset] = next;
        if (next != old)
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
            byte old = _heat[offset];
            int val = old + scaled;
            byte next = (byte)Math.Clamp(val, 0, 255);
            _heat[offset] = next;
            if (next != old)
                MarkHeatDirty(nx, ny);
        });
    }

    private byte[]? _heatTemp;
    private float[]? _heatExchangeDelta;

    public void CoolDown(int decayAmount, float diffuseRate = 0.12f)
    {
        int w = Width, h = Height;
        int len = w * h;

        if (_heatTemp == null || _heatTemp.Length < len)
            _heatTemp = new byte[len];

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int idx = row + x;
                int center = _heat[idx];
                if (center == 0 && x > 0 && x < w - 1 && y > 0 && y < h - 1)
                {
                    int neighborSum = _heat[idx - 1] + _heat[idx + 1] +
                                      _heat[idx - w] + _heat[idx + w];
                    if (neighborSum == 0) { _heatTemp[idx] = 0; continue; }
                }

                int sum = center * 4;
                int cnt = 4;
                if (x > 0) { sum += _heat[idx - 1]; cnt++; }
                if (x < w - 1) { sum += _heat[idx + 1]; cnt++; }
                if (y > 0) { sum += _heat[idx - w]; cnt++; }
                if (y < h - 1) { sum += _heat[idx + w]; cnt++; }

                float blurred = sum / (float)cnt;
                float mixed = center + (blurred - center) * diffuseRate;
                int val = Tweaks.World.EnableMaterialHeatExchange
                    ? (int)(mixed + 0.5f)
                    : (int)(mixed + 0.5f) - decayAmount;
                byte next = (byte)(val < 0 ? 0 : val > 255 ? 255 : val);
                _heatTemp[idx] = next;
                if (next != _heat[idx])
                    MarkHeatDirty(x, y);
            }
        }

        Array.Copy(_heatTemp, _heat, len);

        if (Tweaks.World.EnableMaterialHeatExchange)
            ApplyMaterialHeatExchange();
    }

    public float SampleAverageHeat(Position center, int radius)
    {
        int sum = 0, count = 0;
        ForEachInRadius(center, radius, (nx, ny, _, _) =>
        {
            sum += _heat[ny * Width + nx];
            count++;
        });
        return count > 0 ? sum / (count * 255f) : 0f;
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

    private void ApplyMaterialHeatExchange()
    {
        int w = Width, h = Height, len = w * h;
        if (_heatExchangeDelta == null || _heatExchangeDelta.Length < len)
            _heatExchangeDelta = new float[len];
        Array.Clear(_heatExchangeDelta, 0, len);

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int idx = row + x;
                ThermalMaterial m1 = Pixel.GetThermalMaterial(_data[idx]);
                float t1 = _heat[idx];

                if (x + 1 < w)
                    ExchangePair(idx, idx + 1, m1, t1);
                if (y + 1 < h)
                    ExchangePair(idx, idx + w, m1, t1);
                ExchangeAmbient(idx, m1, t1);
            }
        }

        for (int i = 0; i < len; i++)
        {
            if (_heatExchangeDelta[i] == 0f) continue;
            byte old = _heat[i];
            int nextVal = (int)MathF.Round(old + _heatExchangeDelta[i]);
            byte next = (byte)Math.Clamp(nextVal, 0, 255);
            _heat[i] = next;
            if (next != old)
            {
                int x = i % w;
                int y = i / w;
                MarkHeatDirty(x, y);
            }
        }
    }

    private void ExchangePair(int idxA, int idxB, ThermalMaterial mA, float tA)
    {
        ThermalMaterial mB = Pixel.GetThermalMaterial(_data[idxB]);
        float tB = _heat[idxB];
        float delta = tA - tB;
        if (MathF.Abs(delta) < 0.0001f) return;

        float k = GetConductance(mA, mB);
        float dQ = k * delta * Tweaks.World.ThermalDt;
        if (MathF.Abs(dQ) < 0.0001f) return;

        float cA = GetHeatCapacity(mA);
        float cB = GetHeatCapacity(mB);

        _heatExchangeDelta![idxA] -= dQ / cA;
        _heatExchangeDelta[idxB] += dQ / cB;
    }

    private void ExchangeAmbient(int idx, ThermalMaterial material, float temperature)
    {
        float ambient = Tweaks.World.ThermalAmbientTemperature;
        float k = GetAmbientConductance(material);
        float dQ = k * (ambient - temperature) * Tweaks.World.ThermalDt;
        if (MathF.Abs(dQ) < 0.0001f) return;

        float capacity = GetHeatCapacity(material);
        _heatExchangeDelta![idx] += dQ / capacity;
    }

    private static float GetHeatCapacity(ThermalMaterial material) => material switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalCapacityAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalCapacityDirt,
        _ => Tweaks.World.ThermalCapacityStone,
    };

    private static float GetConductance(ThermalMaterial a, ThermalMaterial b)
    {
        if (a > b) (a, b) = (b, a);
        return (a, b) switch
        {
            (ThermalMaterial.Air, ThermalMaterial.Air) => Tweaks.World.ThermalKAirAir,
            (ThermalMaterial.Air, ThermalMaterial.Dirt) => Tweaks.World.ThermalKAirDirt,
            (ThermalMaterial.Air, ThermalMaterial.Stone) => Tweaks.World.ThermalKAirStone,
            (ThermalMaterial.Dirt, ThermalMaterial.Dirt) => Tweaks.World.ThermalKDirtDirt,
            (ThermalMaterial.Dirt, ThermalMaterial.Stone) => Tweaks.World.ThermalKDirtStone,
            _ => Tweaks.World.ThermalKStoneStone,
        };
    }

    private static float GetAmbientConductance(ThermalMaterial material) => material switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalKAmbientAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalKAmbientDirt,
        _ => Tweaks.World.ThermalKAmbientStone,
    };

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
