namespace Tunnerer.Core.Terrain;

using System.Diagnostics;
using Tunnerer.Core.Config;
using Tunnerer.Core.Types;

public partial class TerrainGrid
{
    private readonly record struct SparseRegion(int MinX, int MinY, int MaxXInclusive, int MaxYInclusive);

    private bool[]? _activeTiles;
    private bool[]? _nextActiveTiles;
    private int _tileSize;
    private int _tileCountX;
    private int _tileCountY;
    private int _tileCount;

    private void EnsureThermalTiles()
    {
        int size = Math.Max(8, _simulationSettings.ThermalActiveTileSize);
        int tcx = (Width + size - 1) / size;
        int tcy = (Height + size - 1) / size;
        int count = tcx * tcy;
        if (_activeTiles != null && _tileSize == size && _tileCount == count)
            return;

        _tileSize = size;
        _tileCountX = tcx;
        _tileCountY = tcy;
        _tileCount = count;
        _activeTiles = new bool[count];
        _nextActiveTiles = new bool[count];
    }

    private int CountActiveTiles()
    {
        if (_activeTiles == null)
            return 0;
        int count = 0;
        for (int i = 0; i < _activeTiles.Length; i++)
            if (_activeTiles[i]) count++;
        return count;
    }

    private void ActivateTileByCell(int x, int y)
    {
        if (_activeTiles == null)
            return;
        int tx = x / _tileSize;
        int ty = y / _tileSize;
        if ((uint)tx >= (uint)_tileCountX || (uint)ty >= (uint)_tileCountY)
            return;
        _activeTiles[tx + ty * _tileCountX] = true;
    }

    private void ActivateNextTileByCell(int x, int y)
    {
        if (_nextActiveTiles == null)
            return;
        int tx = x / _tileSize;
        int ty = y / _tileSize;
        if ((uint)tx >= (uint)_tileCountX || (uint)ty >= (uint)_tileCountY)
            return;
        _nextActiveTiles[tx + ty * _tileCountX] = true;
    }

    private void ClearNextActiveTiles()
    {
        if (_nextActiveTiles != null)
            Array.Clear(_nextActiveTiles, 0, _nextActiveTiles.Length);
    }

    private void SwapActiveTiles()
    {
        if (_activeTiles == null || _nextActiveTiles == null)
            return;
        var tmp = _activeTiles;
        _activeTiles = _nextActiveTiles;
        _nextActiveTiles = tmp;
        Array.Clear(_nextActiveTiles, 0, _nextActiveTiles.Length);
    }

    private List<SparseRegion> BuildSparseRegions()
    {
        var regions = new List<SparseRegion>();
        if (_activeTiles == null)
            return regions;

        bool[] visited = new bool[_activeTiles.Length];
        var queue = new Queue<int>(64);
        for (int tile = 0; tile < _activeTiles.Length; tile++)
        {
            if (!_activeTiles[tile] || visited[tile])
                continue;
            visited[tile] = true;
            queue.Enqueue(tile);
            int minTx = int.MaxValue, minTy = int.MaxValue, maxTx = int.MinValue, maxTy = int.MinValue;

            while (queue.Count > 0)
            {
                int t = queue.Dequeue();
                int tx = t % _tileCountX;
                int ty = t / _tileCountX;
                if (tx < minTx) minTx = tx;
                if (ty < minTy) minTy = ty;
                if (tx > maxTx) maxTx = tx;
                if (ty > maxTy) maxTy = ty;

                TryEnqueue(tx - 1, ty);
                TryEnqueue(tx + 1, ty);
                TryEnqueue(tx, ty - 1);
                TryEnqueue(tx, ty + 1);
            }

            int padMinTx = Math.Max(0, minTx - 1);
            int padMinTy = Math.Max(0, minTy - 1);
            int padMaxTx = Math.Min(_tileCountX - 1, maxTx + 1);
            int padMaxTy = Math.Min(_tileCountY - 1, maxTy + 1);
            int minX = padMinTx * _tileSize;
            int minY = padMinTy * _tileSize;
            int maxX = Math.Min(Width - 1, (padMaxTx + 1) * _tileSize - 1);
            int maxY = Math.Min(Height - 1, (padMaxTy + 1) * _tileSize - 1);
            regions.Add(new SparseRegion(minX, minY, maxX, maxY));

            void TryEnqueue(int nx, int ny)
            {
                if ((uint)nx >= (uint)_tileCountX || (uint)ny >= (uint)_tileCountY)
                    return;
                int ni = nx + ny * _tileCountX;
                if (visited[ni] || !_activeTiles[ni])
                    return;
                visited[ni] = true;
                queue.Enqueue(ni);
            }
        }

        MergeOverlappingRegions(regions);
        return regions;
    }

    private static void MergeOverlappingRegions(List<SparseRegion> regions)
    {
        bool merged;
        do
        {
            merged = false;
            for (int i = 0; i < regions.Count && !merged; i++)
            {
                for (int j = i + 1; j < regions.Count; j++)
                {
                    if (!Intersects(regions[i], regions[j]))
                        continue;
                    var a = regions[i];
                    var b = regions[j];
                    regions[i] = new SparseRegion(
                        Math.Min(a.MinX, b.MinX),
                        Math.Min(a.MinY, b.MinY),
                        Math.Max(a.MaxXInclusive, b.MaxXInclusive),
                        Math.Max(a.MaxYInclusive, b.MaxYInclusive));
                    regions.RemoveAt(j);
                    merged = true;
                    break;
                }
            }
        } while (merged);
    }

    private static bool Intersects(in SparseRegion a, in SparseRegion b)
    {
        return a.MinX <= b.MaxXInclusive && b.MinX <= a.MaxXInclusive &&
               a.MinY <= b.MaxYInclusive && b.MinY <= a.MaxYInclusive;
    }

    private bool IsThermallyActive(float terrainTemperature, float airTemperature)
    {
        float threshold = _simulationSettings.ThermalActiveTemperatureThreshold;
        return MathF.Abs(terrainTemperature) >= threshold || MathF.Abs(airTemperature) >= threshold;
    }

    public bool TryGetThermalTileInfo(out int tileSize, out int tileCountX, out int tileCountY)
    {
        if (_activeTiles == null || _tileCountX <= 0 || _tileCountY <= 0)
        {
            tileSize = 0;
            tileCountX = 0;
            tileCountY = 0;
            return false;
        }

        tileSize = _tileSize;
        tileCountX = _tileCountX;
        tileCountY = _tileCountY;
        return true;
    }

    public bool IsThermalTileActive(int tileX, int tileY)
    {
        if (_activeTiles == null)
            return false;
        if ((uint)tileX >= (uint)_tileCountX || (uint)tileY >= (uint)_tileCountY)
            return false;
        return _activeTiles[tileX + tileY * _tileCountX];
    }

    private void CoolDownSparseRegions(IReadOnlyList<SparseRegion> regions, bool parallel, out TimeSpan markDirty, out float maxAbsDelta)
    {
        markDirty = TimeSpan.Zero;
        maxAbsDelta = 0f;
        if (_heatTemp == null || _nextActiveTiles == null)
            return;

        var workers = new List<RegionWork>(regions.Count);
        for (int i = 0; i < regions.Count; i++)
        {
            var r = regions[i];
            int rw = r.MaxXInclusive - r.MinX + 1;
            int rh = r.MaxYInclusive - r.MinY + 1;
            workers.Add(new RegionWork(i, r, rw, rh, new float[rw * rh], new float[rw * rh]));
        }

        if (parallel)
        {
            var po = new ParallelOptions();
            if (_simulationSettings.ThermalMaxWorkers > 0)
                po.MaxDegreeOfParallelism = _simulationSettings.ThermalMaxWorkers;
            Parallel.ForEach(workers, po, work =>
            {
                _heatEngine.ComputeRegionDeltaConservative(
                    _heatTemperature,
                    _airTemperature,
                    _data,
                    Width,
                    Height,
                    work.Region.MinX,
                    work.Region.MinY,
                    work.Region.MaxXInclusive,
                    work.Region.MaxYInclusive,
                    work.Delta,
                    work.AirDelta);
            });
        }
        else
        {
            for (int i = 0; i < workers.Count; i++)
            {
                var work = workers[i];
                _heatEngine.ComputeRegionDeltaConservative(
                    _heatTemperature,
                    _airTemperature,
                    _data,
                    Width,
                    Height,
                    work.Region.MinX,
                    work.Region.MinY,
                    work.Region.MaxXInclusive,
                    work.Region.MaxYInclusive,
                    work.Delta,
                    work.AirDelta);
            }
        }

        long markStart = Stopwatch.GetTimestamp();
        workers.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        ClearNextActiveTiles();
        for (int i = 0; i < workers.Count; i++)
        {
            var work = workers[i];
            int rw = work.RegionWidth;
            int idx = 0;
            for (int y = work.Region.MinY; y <= work.Region.MaxYInclusive; y++)
            {
                int row = y * Width;
                for (int x = work.Region.MinX; x <= work.Region.MaxXInclusive; x++, idx++)
                {
                    int offset = row + x;
                    float oldTerrain = _heatTemperature[offset];
                    float oldAir = _airTemperature[offset];
                    float next = _heatTemperature[offset] + work.Delta[idx];
                    float nextAir = _airTemperature[offset] + work.AirDelta[idx];
                    ThermalMaterial material = Pixel.GetThermalMaterial(_data[offset]);
                    if (material == ThermalMaterial.Air)
                    {
                        next = nextAir;
                    }
                    else if (material == ThermalMaterial.Base)
                    {
                        next = Tweaks.World.ThermalFixedBaseTemperature;
                    }
                    else if (material == ThermalMaterial.ConstantEnergy)
                    {
                        next = Tweaks.World.ThermalFixedConstantEnergyTemperature;
                    }
                    _heatTemperature[offset] = next;
                    _airTemperature[offset] = nextAir;
                    float deltaTerrain = MathF.Abs(next - oldTerrain);
                    float deltaAir = MathF.Abs(nextAir - oldAir);
                    float cellMaxDelta = MathF.Max(deltaTerrain, deltaAir);
                    if (cellMaxDelta > maxAbsDelta)
                        maxAbsDelta = cellMaxDelta;
                    if (ShouldMarkHeatDirty(_heatTemp[offset], next))
                        MarkHeatDirty(x, y);
                    if (IsThermallyActive(next, nextAir))
                        ActivateNextTileByCell(x, y);
                }
            }
        }
        markDirty = Stopwatch.GetElapsedTime(markStart);
        SwapActiveTiles();
    }

    private readonly record struct RegionWork(
        int Id,
        SparseRegion Region,
        int RegionWidth,
        int RegionHeight,
        float[] Delta,
        float[] AirDelta);
}
