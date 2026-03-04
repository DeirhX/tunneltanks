namespace Tunnerer.Core.Terrain;

using Tunnerer.Core.Config;

public partial class TerrainGrid
{
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
}
