namespace TunnelTanks.Core.Entities.Projectiles;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;

public static class ExplosionFactory
{
    public static List<Projectile> CreateExplosion(Position center, int count, float speed, int frames)
    {
        var result = new List<Projectile>(count);
        var rng = Random.Shared;

        for (int i = 0; i < count; i++)
        {
            float angle = rng.NextSingle() * MathF.Tau;
            float s = rng.NextSingle() * speed * Tweaks.Explosion.SpeedVarianceRange
                    + speed * Tweaks.Explosion.SpeedVarianceBase;
            var dir = new VectorF(MathF.Cos(angle) * s, MathF.Sin(angle) * s);
            int life = rng.Next(0, frames);
            result.Add(Projectile.CreateShrapnel(center, dir, life));
        }
        return result;
    }

    public static List<Projectile> CreateFan(Position center, VectorF baseDirection, float spreadAngle,
        int count, float speed, int frames, ProjectileType type)
    {
        var result = new List<Projectile>(count);
        var rng = Random.Shared;
        float baseAngle = MathF.Atan2(baseDirection.Y, baseDirection.X);

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + (rng.NextSingle() - 0.5f) * spreadAngle;
            var dir = new VectorF(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
            int life = frames;

            result.Add(type switch
            {
                ProjectileType.ConcreteFoam => Projectile.CreateConcreteFoam(center, dir, life),
                ProjectileType.DirtFoam => Projectile.CreateDirtFoam(center, dir, life),
                _ => Projectile.CreateShrapnel(center, dir, life),
            });
        }
        return result;
    }
}
