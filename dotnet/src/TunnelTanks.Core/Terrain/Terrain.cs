namespace TunnelTanks.Core.Terrain;

using TunnelTanks.Core.Types;

public class Terrain
{
    private readonly TerrainPixel[] _data;
    private readonly List<Position> _changeList = new();

    public Size Size { get; }
    public int Width => Size.X;
    public int Height => Size.Y;

    public Terrain(Size size)
    {
        Size = size;
        _data = new TerrainPixel[size.Area];
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

    public bool IsInside(Position pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < Width && pos.Y < Height;

    public int CountDirtNeighbors(Position pos)
    {
        int x = pos.X, y = pos.Y, w = Width;
        int count = 0;
        // 8-neighbor check using raw offsets (no bounds check, caller must ensure interior position)
        if (Pixel.IsDirt(_data[x - 1 + (y - 1) * w])) count++;
        if (Pixel.IsDirt(_data[x     + (y - 1) * w])) count++;
        if (Pixel.IsDirt(_data[x + 1 + (y - 1) * w])) count++;
        if (Pixel.IsDirt(_data[x - 1 + y * w])) count++;
        if (Pixel.IsDirt(_data[x + 1 + y * w])) count++;
        if (Pixel.IsDirt(_data[x - 1 + (y + 1) * w])) count++;
        if (Pixel.IsDirt(_data[x     + (y + 1) * w])) count++;
        if (Pixel.IsDirt(_data[x + 1 + (y + 1) * w])) count++;
        return count;
    }

    public int CountLevelGenNeighbors(Position pos)
    {
        int x = pos.X, y = pos.Y, w = Width;
        return (byte)_data[x - 1 + (y - 1) * w]
             + (byte)_data[x     + (y - 1) * w]
             + (byte)_data[x + 1 + (y - 1) * w]
             + (byte)_data[x - 1 + y * w]
             + (byte)_data[x + 1 + y * w]
             + (byte)_data[x - 1 + (y + 1) * w]
             + (byte)_data[x     + (y + 1) * w]
             + (byte)_data[x + 1 + (y + 1) * w];
    }

    public bool HasLevelGenNeighbor(int x, int y)
    {
        int w = Width;
        if (_data[x - 1 + (y - 1) * w] == TerrainPixel.LevelGenDirt) return true;
        if (_data[x     + (y - 1) * w] == TerrainPixel.LevelGenDirt) return true;
        if (_data[x + 1 + (y - 1) * w] == TerrainPixel.LevelGenDirt) return true;
        if (_data[x - 1 + y * w] == TerrainPixel.LevelGenDirt) return true;
        if (_data[x + 1 + y * w] == TerrainPixel.LevelGenDirt) return true;
        if (_data[x - 1 + (y + 1) * w] == TerrainPixel.LevelGenDirt) return true;
        if (_data[x     + (y + 1) * w] == TerrainPixel.LevelGenDirt) return true;
        if (_data[x + 1 + (y + 1) * w] == TerrainPixel.LevelGenDirt) return true;
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
            MaterializeTerrainParallel();
            return;
        }

        var rng = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        for (int i = 0; i < _data.Length; i++)
        {
            _data[i] = _data[i] switch
            {
                TerrainPixel.LevelGenDirt => rng.Next(2) == 0 ? TerrainPixel.DirtHigh : TerrainPixel.DirtLow,
                TerrainPixel.LevelGenRock => TerrainPixel.Rock,
                TerrainPixel.LevelGenMark => TerrainPixel.Rock,
                _ => _data[i],
            };
        }
    }

    private void MaterializeTerrainParallel()
    {
        int chunkSize = 4096;
        int length = _data.Length;
        int chunks = (length + chunkSize - 1) / chunkSize;

        Parallel.For(0, chunks, chunk =>
        {
            var rng = new Random();
            int from = chunk * chunkSize;
            int to = Math.Min(from + chunkSize, length);
            for (int i = from; i < to; i++)
            {
                _data[i] = _data[i] switch
                {
                    TerrainPixel.LevelGenDirt => rng.Next(2) == 0 ? TerrainPixel.DirtHigh : TerrainPixel.DirtLow,
                    TerrainPixel.LevelGenRock => TerrainPixel.Rock,
                    TerrainPixel.LevelGenMark => TerrainPixel.Rock,
                    _ => _data[i],
                };
            }
        });
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
