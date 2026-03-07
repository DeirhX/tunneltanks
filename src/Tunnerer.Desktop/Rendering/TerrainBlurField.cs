namespace Tunnerer.Desktop.Rendering;

using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

/// <summary>
/// Gaussian blur of the solid/cave mask, producing a smooth signed distance-like field.
/// Values range from -1 (deep cave) to +1 (deep solid), with smooth transitions at boundaries.
/// Shared between the CPU HiRes renderer and GPU aux texture generation.
/// </summary>
public sealed class TerrainBlurField
{
    private const int KernelRadius = 7;
    private const int KernelSize = KernelRadius * 2 + 1;
    private const float GaussianSigma = 5.0f;
    private static readonly float[] GaussKernel;

    static TerrainBlurField()
    {
        GaussKernel = new float[KernelSize * KernelSize];
        for (int ky = -KernelRadius; ky <= KernelRadius; ky++)
            for (int kx = -KernelRadius; kx <= KernelRadius; kx++)
                GaussKernel[(ky + KernelRadius) * KernelSize + (kx + KernelRadius)] =
                    MathF.Exp(-(kx * kx + ky * ky) / (2f * GaussianSigma * GaussianSigma));
    }

    private float[]? _field;
    private int _width;
    private int _height;

    public int Width => _width;
    public int Height => _height;

    public void Rebuild(TerrainGrid terrain)
    {
        int w = terrain.Width, h = terrain.Height;
        if (_field == null || _width != w || _height != h)
        {
            _field = new float[w * h];
            _width = w;
            _height = h;
        }

        for (int cy = 0; cy < h; cy++)
            for (int cx = 0; cx < w; cx++)
                _field[cy * w + cx] = ComputeCell(terrain, cx, cy, w, h);
    }

    public void UpdateDirty(TerrainGrid terrain, IReadOnlyList<Position> dirtyCells)
    {
        if (_field == null) { Rebuild(terrain); return; }

        int w = _width, h = _height;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            var p = dirtyCells[i];
            for (int dy = -KernelRadius; dy <= KernelRadius; dy++)
            {
                int ny = p.Y + dy;
                if ((uint)ny >= (uint)h) continue;
                for (int dx = -KernelRadius; dx <= KernelRadius; dx++)
                {
                    int nx = p.X + dx;
                    if ((uint)nx >= (uint)w) continue;
                    _field[ny * w + nx] = ComputeCell(terrain, nx, ny, w, h);
                }
            }
        }
    }

    public void UpdateRect(TerrainGrid terrain, int minX, int minY, int maxX, int maxY)
    {
        if (_field == null) { Rebuild(terrain); return; }

        int w = _width, h = _height;
        int padMinX = Math.Max(0, minX - KernelRadius);
        int padMinY = Math.Max(0, minY - KernelRadius);
        int padMaxX = Math.Min(w - 1, maxX + KernelRadius);
        int padMaxY = Math.Min(h - 1, maxY + KernelRadius);

        int rowCount = padMaxY - padMinY + 1;
        if (rowCount >= 32)
        {
            float[] field = _field;
            Parallel.For(padMinY, padMaxY + 1, cy =>
            {
                for (int cx = padMinX; cx <= padMaxX; cx++)
                    field[cy * w + cx] = ComputeCell(terrain, cx, cy, w, h);
            });
        }
        else
        {
            for (int cy = padMinY; cy <= padMaxY; cy++)
                for (int cx = padMinX; cx <= padMaxX; cx++)
                    _field[cy * w + cx] = ComputeCell(terrain, cx, cy, w, h);
        }
    }

    public float Sample(int x, int y)
    {
        if (_field == null || (uint)x >= (uint)_width || (uint)y >= (uint)_height) return 1f;
        return _field[y * _width + x];
    }

    /// <summary>
    /// Convert the field value at (x, y) from [-1,+1] to a byte [0,255] for the aux G channel.
    /// </summary>
    public byte SampleAsByte(int x, int y)
    {
        float v = Sample(x, y);
        int b = (int)((v * 0.5f + 0.5f) * 255f + 0.5f);
        return (byte)(b < 0 ? 0 : b > 255 ? 255 : b);
    }

    private static float ComputeCell(TerrainGrid terrain, int cx, int cy, int w, int h)
    {
        float sum = 0f, wSum = 0f;
        for (int ky = -KernelRadius; ky <= KernelRadius; ky++)
        {
            int ny = cy + ky;
            if ((uint)ny >= (uint)h) continue;
            int rowOff = ny * w;
            for (int kx = -KernelRadius; kx <= KernelRadius; kx++)
            {
                int nx = cx + kx;
                if ((uint)nx >= (uint)w) continue;
                float gw = GaussKernel[(ky + KernelRadius) * KernelSize + (kx + KernelRadius)];
                sum += gw * (IsSolidTerrain(terrain.GetPixelRaw(rowOff + nx)) ? 1f : -1f);
                wSum += gw;
            }
        }
        return sum / wSum;
    }

    private static bool IsSolidTerrain(TerrainPixel p)
    {
        if (p == TerrainPixel.Blank) return false;
        if (Pixel.IsScorched(p)) return false;
        return true;
    }
}
