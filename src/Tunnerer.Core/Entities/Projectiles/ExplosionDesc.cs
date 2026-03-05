namespace Tunnerer.Core.Entities.Projectiles;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;

public static class ExplosionFactory
{
    public static List<Projectile> CreateExplosion(
        Position center,
        ExplosionParams p,
        ref FastRandom rng,
        float shrapnelHeatScale = 1f)
    {
        var result = new List<Projectile>(p.ShrapnelCount);

        for (int i = 0; i < p.ShrapnelCount; i++)
        {
            float angle = rng.NextSingle() * MathF.Tau;
            float s = rng.NextSingle() * p.Speed * Tweaks.Explosion.SpeedVarianceRange
                    + p.Speed * Tweaks.Explosion.SpeedVarianceBase;
            var dir = new VectorF(MathF.Cos(angle) * s, MathF.Sin(angle) * s);
            int life = rng.NextInt(0, p.Frames);
            result.Add(Projectile.CreateShrapnel(center, dir, life, shrapnelHeatScale));
        }
        return result;
    }

    public static List<Projectile> CreateFan(Position center, VectorF baseDirection, float spreadAngle,
        ExplosionParams p, ProjectileType type, ref FastRandom rng)
    {
        var result = new List<Projectile>(p.ShrapnelCount);
        float baseAngle = MathF.Atan2(baseDirection.Y, baseDirection.X);

        for (int i = 0; i < p.ShrapnelCount; i++)
        {
            float angle = baseAngle + (rng.NextSingle() - 0.5f) * spreadAngle;
            var dir = new VectorF(MathF.Cos(angle) * p.Speed, MathF.Sin(angle) * p.Speed);
            int life = p.Frames;

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
