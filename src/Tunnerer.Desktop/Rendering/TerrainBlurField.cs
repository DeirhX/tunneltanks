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
    private const float GaussianSigma = 1.8f;
    private static readonly float[] Gauss5x5;

    static TerrainBlurField()
    {
        Gauss5x5 = new float[25];
        for (int ky = -2; ky <= 2; ky++)
            for (int kx = -2; kx <= 2; kx++)
                Gauss5x5[(ky + 2) * 5 + (kx + 2)] =
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
            for (int dy = -2; dy <= 2; dy++)
            {
                int ny = p.Y + dy;
                if ((uint)ny >= (uint)h) continue;
                for (int dx = -2; dx <= 2; dx++)
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
        int padMinX = Math.Max(0, minX - 2);
        int padMinY = Math.Max(0, minY - 2);
        int padMaxX = Math.Min(w - 1, maxX + 2);
        int padMaxY = Math.Min(h - 1, maxY + 2);

        for (int cy = padMinY; cy <= padMaxY; cy++)
            for (int cx = padMinX; cx <= padMaxX; cx++)
                _field[cy * w + cx] = ComputeCell(terrain, cx, cy, w, h);
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
        for (int ky = -2; ky <= 2; ky++)
        {
            int ny = cy + ky;
            if ((uint)ny >= (uint)h) continue;
            int rowOff = ny * w;
            for (int kx = -2; kx <= 2; kx++)
            {
                int nx = cx + kx;
                if ((uint)nx >= (uint)w) continue;
                float gw = Gauss5x5[(ky + 2) * 5 + (kx + 2)];
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
