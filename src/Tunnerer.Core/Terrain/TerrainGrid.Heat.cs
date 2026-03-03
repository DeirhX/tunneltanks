namespace Tunnerer.Core.Terrain;

using System.Diagnostics;
using Tunnerer.Core.Config;
using Tunnerer.Core.Thermal;
using Tunnerer.Core.Types;

public partial class TerrainGrid
{
    public readonly struct CoolDownProfile
    {
        public CoolDownProfile(TimeSpan prep, TimeSpan simulate, TimeSpan markDirty, TimeSpan writeBack)
        {
            Prep = prep;
            Simulate = simulate;
            MarkDirty = markDirty;
            WriteBack = writeBack;
        }

        public TimeSpan Prep { get; }
        public TimeSpan Simulate { get; }
        public TimeSpan MarkDirty { get; }
        public TimeSpan WriteBack { get; }
    }

    private readonly float[] _heatTemperature;
    private readonly TerrainHeatEngine _heatEngine;
    private readonly SimulationSettings _simulationSettings;
    private float[]? _heatTemp;
    private bool _hasHeatDirtyRect;
    private int _heatDirtyMinX;
    private int _heatDirtyMinY;
    private int _heatDirtyMaxX;
    private int _heatDirtyMaxY;

    public CoolDownProfile LastCoolDownProfile { get; private set; }

    public float GetHeatTemperature(int offset)
        => (uint)offset < (uint)_heatTemperature.Length ? _heatTemperature[offset] : 0f;

    public float GetHeatTemperature(Position pos)
    {
        if ((uint)pos.X >= (uint)Width || (uint)pos.Y >= (uint)Height)
            return 0f;
        int offset = pos.X + pos.Y * Width;
        Debug.Assert((uint)offset < (uint)_heatTemperature.Length, "Validated position must map to a valid offset.");
        return _heatEngine.GetTemperatureAt(_heatTemperature, _heatTemperature.Length, offset);
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
        });
    }

    public void CoolDown(int decayAmount, float diffuseRate = 0.12f)
    {
        int w = Width, h = Height;
        int len = w * h;
        TimeSpan prep = TimeSpan.Zero;
        TimeSpan simulate = TimeSpan.Zero;
        TimeSpan markDirty = TimeSpan.Zero;
        TimeSpan writeBack = TimeSpan.Zero;

        if (_simulationSettings.EnableMaterialHeatExchange)
        {
            if (_heatTemp == null || _heatTemp.Length < len)
                _heatTemp = new float[len];
            long t0 = Stopwatch.GetTimestamp();
            Array.Copy(_heatTemperature, _heatTemp, len);
            prep = Stopwatch.GetElapsedTime(t0);
            t0 = Stopwatch.GetTimestamp();
            _heatEngine.Step(_heatTemperature, _data, w, h);
            simulate = Stopwatch.GetElapsedTime(t0);
            t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < len; i++)
            {
                if (!ShouldMarkHeatDirty(_heatTemp[i], _heatTemperature[i])) continue;
                int x = i % w;
                int y = i / w;
                MarkHeatDirty(x, y);
            }
            markDirty = Stopwatch.GetElapsedTime(t0);
            LastCoolDownProfile = new CoolDownProfile(prep, simulate, markDirty, writeBack);
            return;
        }

        if (_heatTemp == null || _heatTemp.Length < len)
            _heatTemp = new float[len];

        long legacyStart = Stopwatch.GetTimestamp();
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
                if (ShouldMarkHeatDirty(_heatTemperature[idx], next))
                    MarkHeatDirty(x, y);
            }
        }
        simulate = Stopwatch.GetElapsedTime(legacyStart);

        long copyStart = Stopwatch.GetTimestamp();
        Array.Copy(_heatTemp, _heatTemperature, len);
        writeBack = Stopwatch.GetElapsedTime(copyStart);
        LastCoolDownProfile = new CoolDownProfile(prep, simulate, markDirty, writeBack);
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
            if (ShouldMarkHeatDirty(old, next))
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

    private static bool ShouldMarkHeatDirty(float oldTemperature, float newTemperature)
    {
        return HeatToByte(oldTemperature) != HeatToByte(newTemperature);
    }

    private static byte HeatToByte(float temperature)
    {
        return (byte)Math.Clamp((int)MathF.Round(temperature), 0, 255);
    }
}
