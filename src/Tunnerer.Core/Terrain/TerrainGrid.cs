namespace Tunnerer.Core.Terrain;

using System.Diagnostics;
using Tunnerer.Core.Thermal;
using Tunnerer.Core.Types;

public partial class TerrainGrid
{
    private readonly TerrainPixel[] _data;
    private readonly float[] _airTemperature;
    private readonly int[] _neighborOffsets;
    private readonly List<Position> _changeList = new();

    public Size Size { get; }
    public int Width => Size.X;
    public int Height => Size.Y;

    public TerrainGrid(Size size)
        : this(size, Config.SimulationSettings.FromTweaks())
    {
    }

    public TerrainGrid(Size size, Config.SimulationSettings simulationSettings)
    {
        _simulationSettings = simulationSettings;
        Size = size;
        _data = new TerrainPixel[size.Area];
        _heatTemperature = new float[size.Area];
        _airTemperature = new float[size.Area];
        _heatEngine = new TerrainHeatEngine(simulationSettings);
        _neighborOffsets = BuildNeighborOffsets(size.X);
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
        AssertHasNeighborMargin(pos.X, pos.Y);
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
        AssertHasNeighborMargin(pos.X, pos.Y);
        int offset = pos.X + pos.Y * Width;
        int sum = 0;
        for (int i = 0; i < 8; i++)
            sum += (byte)_data[offset + _neighborOffsets[i]];
        return sum;
    }

    public bool HasLevelGenNeighbor(int x, int y)
    {
        AssertHasNeighborMargin(x, y);
        int offset = x + y * Width;
        for (int i = 0; i < 8; i++)
            if (_data[offset + _neighborOffsets[i]] == TerrainPixel.LevelGenDirt) return true;
        return false;
    }

    [Conditional("DEBUG")]
    private void AssertHasNeighborMargin(int x, int y)
    {
        Debug.Assert(
            x > 0 && x < Width - 1 && y > 0 && y < Height - 1,
            "Neighbor queries require an interior position (not on map boundary).");
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

    public IReadOnlyList<Position> GetChangeList() => _changeList;
    public void ClearChangeList() => _changeList.Clear();

    public void Fill(TerrainPixel value) => Array.Fill(_data, value);

    public ReadOnlySpan<TerrainPixel> Data => _data;
}
