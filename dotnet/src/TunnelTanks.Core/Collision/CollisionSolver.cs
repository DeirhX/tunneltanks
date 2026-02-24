namespace TunnelTanks.Core.Collision;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Terrain;

public enum CollisionType { None, Dirt, Blocked }

public class CollisionSolver
{
    private readonly TerrainGrid _terrain;

    public CollisionSolver(TerrainGrid terrain)
    {
        _terrain = terrain;
    }

    public CollisionType TestPixel(Position pos)
    {
        var pix = _terrain.GetPixel(pos);
        if (Pixel.IsBlockingCollision(pix)) return CollisionType.Blocked;
        if (Pixel.IsSoftCollision(pix)) return CollisionType.Dirt;
        return CollisionType.None;
    }

    public CollisionType TestShape(Position center, byte[] shape, int shapeW, int shapeH)
    {
        var result = CollisionType.None;
        int cx = shapeW / 2, cy = shapeH / 2;

        for (int sy = 0; sy < shapeH; sy++)
            for (int sx = 0; sx < shapeW; sx++)
            {
                if (shape[sx + sy * shapeW] == 0) continue;
                var worldPos = new Position(center.X - cx + sx, center.Y - cy + sy);
                var pix = _terrain.GetPixel(worldPos);

                if (Pixel.IsBlockingCollision(pix))
                    return CollisionType.Blocked;
                if (Pixel.IsSoftCollision(pix))
                    result = CollisionType.Dirt;
            }
        return result;
    }
}
