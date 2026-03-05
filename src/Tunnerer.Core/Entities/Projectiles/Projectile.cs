namespace Tunnerer.Core.Entities.Projectiles;

using Tunnerer.Core.Types;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Config;

public enum ProjectileType { Bullet, Shrapnel, ConcreteFoam, DirtFoam }

public class Projectile
{
    public PositionF Position { get; set; }
    public VectorF Speed { get; init; }
    public bool IsAlive { get; set; } = true;
    public ProjectileType Type { get; init; }
    public int Life { get; set; }
    public int OwnerColor { get; init; } = -1;
    public float HeatScale { get; init; } = 1f;

    public static Projectile CreateBullet(Position pos, VectorF speed, int ownerColor) => new()
    {
        Position = (PositionF)pos,
        Speed = speed,
        Type = ProjectileType.Bullet,
        IsAlive = true,
        OwnerColor = ownerColor,
    };

    public static Projectile CreateShrapnel(Position pos, VectorF speed, int life, float heatScale = 1f) => new()
    {
        Position = (PositionF)pos,
        Speed = speed,
        Type = ProjectileType.Shrapnel,
        Life = life,
        IsAlive = true,
        HeatScale = heatScale,
    };

    public static Projectile CreateConcreteFoam(Position pos, VectorF speed, int life) => new()
    {
        Position = (PositionF)pos,
        Speed = speed,
        Type = ProjectileType.ConcreteFoam,
        Life = life,
        IsAlive = true,
    };

    public static Projectile CreateDirtFoam(Position pos, VectorF speed, int life) => new()
    {
        Position = (PositionF)pos,
        Speed = speed,
        Type = ProjectileType.DirtFoam,
        Life = life,
        IsAlive = true,
    };
}
