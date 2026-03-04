namespace Tunnerer.Core.Collision;

using Tunnerer.Core.Types;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Entities.Machines;
using Tunnerer.Core.Rendering;

public enum CollisionType { None, Dirt, Blocked }

/// <summary>
/// Central collision API. Wraps terrain, tanks, and machines behind a single
/// visitor-based query. Uses <see cref="WorldSectors"/> for broad-phase on
/// entity queries so projectile-vs-tank checks are O(local) not O(n).
/// </summary>
public class CollisionSolver
{
    private readonly TerrainGrid _terrain;
    private readonly WorldSectors _tankSectors;
    private TankList? _tanks;
    private MachineList? _machines;

    public TerrainGrid Terrain => _terrain;

    public CollisionSolver(TerrainGrid terrain)
    {
        _terrain = terrain;
        _tankSectors = new WorldSectors(terrain.Size);
    }

    /// <summary>
    /// Rebuilds the broad-phase sector index for the current frame.
    /// Call once at the start of each simulation step.
    /// </summary>
    public void Update(TankList tanks, MachineList machines)
    {
        _tanks = tanks;
        _machines = machines;

        _tankSectors.Clear();
        for (int i = 0; i < tanks.Tanks.Count; i++)
        {
            var t = tanks.Tanks[i];
            if (!t.IsDead)
                _tankSectors.AddEntity(_tankSectors.SectorIdForPosition(t.Position), i);
        }
    }

    /// <summary>
    /// Tests a single point against terrain, tanks, and/or machines.
    /// Supply only the callbacks you care about; null callbacks are skipped.
    /// Callbacks receive the hit entity and return true to signal a hit (stops further checks).
    /// Tank checks use sector-based broad-phase; machine checks are linear (few entities).
    /// </summary>
    public bool TestPoint(Position pos,
        Func<Tank, bool>? onTank = null,
        Func<Machine, bool>? onMachine = null,
        Func<TerrainPixel, bool>? onTerrain = null)
    {
        if (onTank != null && _tanks != null)
        {
            int halfW = TankSprites.SpriteWidth / 2;
            int halfH = TankSprites.SpriteHeight / 2;

            bool tankHit = _tankSectors.ForEachNearbyEntity(pos, idx =>
            {
                var tank = _tanks.Tanks[idx];
                if (tank.IsDead) return false;
                if (Math.Abs(pos.X - tank.Position.X) > halfW ||
                    Math.Abs(pos.Y - tank.Position.Y) > halfH) return false;
                if (!IsTankSpritePixelSolid(tank, pos)) return false;
                return onTank(tank);
            });
            if (tankHit) return true;
        }

        if (onMachine != null && _machines != null)
        {
            foreach (var m in _machines.Machines)
            {
                if (!m.IsAlive || !m.IsBlockingCollision) continue;
                if (m.TestCollide(pos) && onMachine(m)) return true;
            }
        }

        if (onTerrain != null && _terrain.IsInside(pos))
        {
            var pix = _terrain.GetPixelRaw(pos);
            if (onTerrain(pix)) return true;
        }

        return false;
    }

    private static bool IsTankSpritePixelSolid(Tank tank, Position pos)
    {
        int dir = Math.Clamp(tank.Direction, 0, TankSprites.DirectionCount - 1);
        var sprite = TankSprites.Sprites[dir];
        int w = TankSprites.SpriteWidth;
        int h = TankSprites.SpriteHeight;
        int cx = w / 2;
        int cy = h / 2;

        int sx = pos.X - tank.Position.X + cx;
        int sy = pos.Y - tank.Position.Y + cy;
        if ((uint)sx >= (uint)w || (uint)sy >= (uint)h)
            return false;

        return sprite[sx + sy * w] != 0;
    }

    // --- Convenience methods for terrain-only queries (backward compatible) ---

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
