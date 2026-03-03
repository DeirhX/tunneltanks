namespace Tunnerer.Core.Terrain;

using Tunnerer.Core.Types;

public partial class TerrainGrid
{
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
}
