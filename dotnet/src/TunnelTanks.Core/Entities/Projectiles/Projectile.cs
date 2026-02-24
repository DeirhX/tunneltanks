namespace TunnelTanks.Core.Entities.Projectiles;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Config;

public enum ProjectileType { Bullet, Shrapnel, ConcreteFoam, DirtFoam }

public class Projectile
{
    public PositionF Position;
    public VectorF Speed;
    public bool IsAlive = true;
    public ProjectileType Type;
    public int Life;
    public int OwnerColor = -1;

    public static Projectile CreateBullet(Position pos, VectorF speed, int ownerColor) => new()
    {
        Position = (PositionF)pos,
        Speed = speed,
        Type = ProjectileType.Bullet,
        IsAlive = true,
        OwnerColor = ownerColor,
    };

    public static Projectile CreateShrapnel(Position pos, VectorF speed, int life) => new()
    {
        Position = (PositionF)pos,
        Speed = speed,
        Type = ProjectileType.Shrapnel,
        Life = life,
        IsAlive = true,
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
