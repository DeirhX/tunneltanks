namespace Tunnerer.Core.Rendering;

using Tunnerer.Core.Entities;
using Tunnerer.Core.Entities.Links;
using Tunnerer.Core.Entities.Machines;
using Tunnerer.Core.Entities.Projectiles;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

public readonly record struct TankRenderState(
    Position Position,
    int Color,
    int Direction,
    bool IsDead,
    DirectionF TurretDirection);

public readonly record struct ProjectileRenderState(
    Position Position,
    ProjectileType Type,
    bool IsAlive);

public readonly record struct MachineRenderState(
    Position Position,
    MachineType Type,
    MachineState State,
    bool IsAlive);

public readonly record struct LinkRenderState(
    Position From,
    Position To,
    LinkType Type,
    bool IsAlive);

public readonly record struct SpriteRenderState(
    Position Position,
    SpriteType Type,
    bool IsAlive);

public readonly struct TerrainRenderSnapshot
{
    private readonly TerrainGrid _terrain;

    public TerrainRenderSnapshot(TerrainGrid terrain)
    {
        _terrain = terrain;
    }

    public int Width => _terrain.Width;
    public int Height => _terrain.Height;
    public ReadOnlySpan<TerrainPixel> Pixels => _terrain.Data;

    public bool TryGetHeatDirtyRect(out Rect dirtyRect)
        => _terrain.TryGetHeatDirtyRect(out dirtyRect);
}
