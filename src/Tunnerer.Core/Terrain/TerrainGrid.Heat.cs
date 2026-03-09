namespace Tunnerer.Core.Terrain;

using System.Diagnostics;
using Tunnerer.Core.Config;
using Tunnerer.Core.Thermal;
using Tunnerer.Core.Types;

public partial class TerrainGrid
{
    public readonly struct CoolDownProfile
    {
        public CoolDownProfile(
            TimeSpan prep,
            TimeSpan simulate,
            TimeSpan markDirty,
            TimeSpan writeBack,
            int activeTiles,
            int regionCount,
            bool usedSparse,
            bool usedParallel)
        {
            Prep = prep;
            Simulate = simulate;
            MarkDirty = markDirty;
            WriteBack = writeBack;
            ActiveTiles = activeTiles;
            RegionCount = regionCount;
            UsedSparse = usedSparse;
            UsedParallel = usedParallel;
        }

        public TimeSpan Prep { get; }
        public TimeSpan Simulate { get; }
        public TimeSpan MarkDirty { get; }
        public TimeSpan WriteBack { get; }
        public int ActiveTiles { get; }
        public int RegionCount { get; }
        public bool UsedSparse { get; }
        public bool UsedParallel { get; }
    }

    private readonly float[] _heatTemperature;
    private readonly TerrainHeatEngine _heatEngine;
    private readonly SimulationSettings _simulationSettings;
    private float[]? _heatTemp;
    private float[]? _airTemp;
    private bool _hasHeatDirtyRect;
    private int _heatDirtyMinX;
    private int _heatDirtyMinY;
    private int _heatDirtyMaxX;
    private int _heatDirtyMaxY;
    private int _coolDownFrameCounter;
    private int _noActiveTileFrameCount;
    private int _saturatedCoverageFrameCount;
    private bool _thermalWakeRequested = true;
    private float _lastCoolDownMaxDelta;

    public CoolDownProfile LastCoolDownProfile { get; private set; }

    public float GetHeatTemperature(int offset)
        => (uint)offset < (uint)_heatTemperature.Length ? _heatTemperature[offset] : 0f;

    public float GetAirTemperature(int offset)
        => (uint)offset < (uint)_airTemperature.Length ? _airTemperature[offset] : 0f;

    public float GetHeatTemperature(Position pos)
    {
        if ((uint)pos.X >= (uint)Width || (uint)pos.Y >= (uint)Height)
            return 0f;
        int offset = pos.X + pos.Y * Width;
        Debug.Assert((uint)offset < (uint)_heatTemperature.Length, "Validated position must map to a valid offset.");
        return _heatEngine.GetTemperatureAt(_heatTemperature, _heatTemperature.Length, offset);
    }

    public float GetAirTemperature(Position pos)
    {
        if ((uint)pos.X >= (uint)Width || (uint)pos.Y >= (uint)Height)
            return 0f;
        int offset = pos.X + pos.Y * Width;
        Debug.Assert((uint)offset < (uint)_airTemperature.Length, "Validated position must map to a valid offset.");
        return _heatEngine.GetTemperatureAt(_airTemperature, _airTemperature.Length, offset);
    }

    public void AddHeat(Position pos, int amount)
    {
        if ((uint)pos.X >= (uint)Width || (uint)pos.Y >= (uint)Height)
            return;
        int offset = pos.X + pos.Y * Width;
        Debug.Assert((uint)offset < (uint)_heatTemperature.Length, "Validated position must map to a valid offset.");
        float old = _heatTemperature[offset];
        _heatEngine.AddEnergyAt(_heatTemperature, offset, amount);
        float next = _heatTemperature[offset];
        if (ShouldMarkHeatDirty(old, next))
            MarkHeatDirty(pos.X, pos.Y);
        if (amount != 0)
        {
            ActivateTileByCell(pos.X, pos.Y);
            RequestThermalWake();
        }
        CommitPixel(pos);
    }

    public void AddHeatRadius(Position center, int amount, int radius)
    {
        Debug.Assert(radius >= 0, "Heat radius is expected to be non-negative.");
        if (radius <= 0)
        {
            AddHeat(center, amount);
            return;
        }

        int radiusSq = radius * radius;
        bool anyApplied = false;
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
            if (ShouldMarkHeatDirty(old, next))
                MarkHeatDirty(nx, ny);
            ActivateTileByCell(nx, ny);
            anyApplied = true;
        });
        if (anyApplied)
            RequestThermalWake();
    }

    public void CoolDown(float artificialCoolingPerFrame = 0f)
    {
        int w = Width, h = Height;
        int len = w * h;
        TimeSpan prep = TimeSpan.Zero;
        TimeSpan simulate = TimeSpan.Zero;
        TimeSpan markDirty = TimeSpan.Zero;
        TimeSpan writeBack = TimeSpan.Zero;
        int activeTiles = 0;
        int regionCount = 0;
        bool usedSparse = false;
        bool usedParallel = false;
        float maxAbsDelta = 0f;

        EnsureThermalTiles();
        _coolDownFrameCounter++;
        activeTiles = CountActiveTiles();
        float coverage = _tileCount <= 0 ? 1f : activeTiles / (float)_tileCount;

        if (activeTiles == 0)
            _noActiveTileFrameCount++;
        else
            _noActiveTileFrameCount = 0;

        if (coverage >= Tweaks.World.ThermalSaturationCoverageThreshold)
            _saturatedCoverageFrameCount++;
        else
            _saturatedCoverageFrameCount = 0;

        bool shouldSleepNoActive =
            artificialCoolingPerFrame <= 0f &&
            _noActiveTileFrameCount >= Tweaks.World.ThermalNoActiveSleepFrames;
        bool shouldSleepQuietDelta =
            !_thermalWakeRequested &&
            _lastCoolDownMaxDelta < _simulationSettings.ThermalMaxDeltaSleepThreshold;
        bool shouldThrottleSaturation =
            _saturatedCoverageFrameCount >= Tweaks.World.ThermalSaturationFramesBeforeThrottle &&
            (_coolDownFrameCounter % Math.Max(1, Tweaks.World.ThermalSaturationStepIntervalFrames)) != 0;

        if (shouldSleepNoActive || shouldSleepQuietDelta || shouldThrottleSaturation)
        {
            if (shouldSleepQuietDelta)
            {
                ClearThermalActiveTiles();
                activeTiles = 0;
            }
            LastCoolDownProfile = new CoolDownProfile(
                prep: TimeSpan.Zero,
                simulate: TimeSpan.Zero,
                markDirty: TimeSpan.Zero,
                writeBack: TimeSpan.Zero,
                activeTiles: activeTiles,
                regionCount: 0,
                usedSparse: false,
                usedParallel: false);
            return;
        }

        if (_heatTemp == null || _heatTemp.Length < len)
            _heatTemp = new float[len];
        if (_airTemp == null || _airTemp.Length < len)
            _airTemp = new float[len];
        long t0 = Stopwatch.GetTimestamp();
        Array.Copy(_heatTemperature, _heatTemp, len);
        Array.Copy(_airTemperature, _airTemp, len);
        prep = Stopwatch.GetElapsedTime(t0);

        bool canUseSparse = activeTiles > 0;
        bool fallbackToFull = !canUseSparse || coverage >= _simulationSettings.ThermalSparseFallbackCoverage;

        if (fallbackToFull)
        {
            t0 = Stopwatch.GetTimestamp();
            _heatEngine.StepConservative(_heatTemperature, _airTemperature, _data, w, h);
            simulate = Stopwatch.GetElapsedTime(t0);
            t0 = Stopwatch.GetTimestamp();
            ClearNextActiveTiles();
            for (int i = 0; i < len; i++)
            {
                float deltaTerrain = MathF.Abs(_heatTemperature[i] - _heatTemp[i]);
                float deltaAir = MathF.Abs(_airTemperature[i] - _airTemp[i]);
                float cellMaxDelta = MathF.Max(deltaTerrain, deltaAir);
                if (cellMaxDelta > maxAbsDelta)
                    maxAbsDelta = cellMaxDelta;
                if (ShouldMarkHeatDirty(_heatTemp[i], _heatTemperature[i]))
                {
                    int x = i % w;
                    int y = i / w;
                    MarkHeatDirty(x, y);
                }
                if (IsThermallyActive(_heatTemperature[i], _airTemperature[i]))
                {
                    ActivateNextTileByCell(i % w, i / w);
                }
            }
            markDirty = Stopwatch.GetElapsedTime(t0);
            SwapActiveTiles();
        }
        else
        {
            usedSparse = true;
            var regions = BuildSparseRegions();
            regionCount = regions.Count;
            bool parallel = regions.Count >= _simulationSettings.ThermalParallelRegionThreshold;
            usedParallel = parallel;
            t0 = Stopwatch.GetTimestamp();
            CoolDownSparseRegions(regions, parallel, out markDirty, out float sparseMaxDelta);
            if (sparseMaxDelta > maxAbsDelta)
                maxAbsDelta = sparseMaxDelta;
            simulate = Stopwatch.GetElapsedTime(t0) - markDirty;
        }

        if (artificialCoolingPerFrame > 0f)
        {
            t0 = Stopwatch.GetTimestamp();
            ClearNextActiveTiles();
            for (int i = 0; i < len; i++)
            {
                if (Pixel.GetThermalMaterial(_data[i]) != ThermalMaterial.Dirt)
                {
                    if (IsThermallyActive(_heatTemperature[i], _airTemperature[i]))
                        ActivateNextTileByCell(i % w, i / w);
                    continue;
                }

                float old = _heatTemperature[i];
                float next = MathF.Max(0f, old - artificialCoolingPerFrame);
                if (next != old)
                {
                    _heatTemperature[i] = next;
                    float deltaTerrain = MathF.Abs(next - old);
                    if (deltaTerrain > maxAbsDelta)
                        maxAbsDelta = deltaTerrain;
                    if (ShouldMarkHeatDirty(old, next))
                    {
                        int x = i % w;
                        int y = i / w;
                        MarkHeatDirty(x, y);
                    }
                    if (IsThermallyActive(_heatTemperature[i], _airTemperature[i]))
                    {
                        ActivateNextTileByCell(i % w, i / w);
                    }
                }
                if (IsThermallyActive(next, _airTemperature[i]))
                {
                    ActivateNextTileByCell(i % w, i / w);
                }
            }
            markDirty += Stopwatch.GetElapsedTime(t0);
            SwapActiveTiles();
        }
        _lastCoolDownMaxDelta = maxAbsDelta;
        _thermalWakeRequested = false;
        LastCoolDownProfile = new CoolDownProfile(prep, simulate, markDirty, writeBack, activeTiles, regionCount, usedSparse, usedParallel);
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

    public float SampleAverageAirTemperature(Position center, int radius)
    {
        float sum = 0f;
        int count = 0;
        ForEachInRadius(center, radius, (nx, ny, _, _) =>
        {
            int idx = ny * Width + nx;
            sum += _heatEngine.GetTemperatureAt(_airTemperature, _airTemperature.Length, idx);
            count++;
        });
        return count > 0 ? sum / count : 0f;
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
            if (ShouldMarkHeatDirty(old, next))
                MarkHeatDirty(nx, ny);
            ActivateTileByCell(nx, ny);
            RequestThermalWake();
            applied += (int)MathF.Round(next - old);
        });

        return applied;
    }

    public int AddAirHeatTotalInRadiusArea(Position center, int radius, int totalAmount)
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
            float old = _airTemperature[offset];
            _heatEngine.AddEnergyAt(_airTemperature, offset, delta);
            float next = _airTemperature[offset];
            if (IsThermallyActive(_heatTemperature[offset], next))
                ActivateTileByCell(nx, ny);
            RequestThermalWake();
            applied += (int)MathF.Round(next - old);
        });

        return applied;
    }

    public double SumTotalThermalEnergy()
    {
        return _heatEngine.SumTotalEnergy(_heatTemperature, _airTemperature, _data);
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

    private void RequestThermalWake()
    {
        _thermalWakeRequested = true;
    }

    private void ClearThermalActiveTiles()
    {
        if (_activeTiles != null)
            Array.Clear(_activeTiles, 0, _activeTiles.Length);
        if (_nextActiveTiles != null)
            Array.Clear(_nextActiveTiles, 0, _nextActiveTiles.Length);
    }

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

    private static bool ShouldMarkHeatDirty(float oldTemperature, float newTemperature)
    {
        return HeatToByte(oldTemperature) != HeatToByte(newTemperature);
    }

    public const float HeatByteScale = 4.0f;

    private static byte HeatToByte(float temperature)
    {
        return (byte)Math.Clamp((int)MathF.Round(temperature / HeatByteScale), 0, 255);
    }
}
