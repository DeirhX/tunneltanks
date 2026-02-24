namespace TunnelTanks.Core.Entities.Projectiles;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Entities;
using TunnelTanks.Core.Resources;

public class ProjectileList
{
    private readonly List<Projectile> _projectiles = new();
    private readonly List<Projectile> _pending = new();

    public int Count => _projectiles.Count + _pending.Count;
    public void Add(Projectile p) => _pending.Add(p);
    public void AddRange(IEnumerable<Projectile> items) => _pending.AddRange(items);

    public void RemoveAll()
    {
        _projectiles.Clear();
        _pending.Clear();
    }

    public void Advance(Terrain terrain, TankList? tankList)
    {
        _projectiles.AddRange(_pending);
        _pending.Clear();

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            if (!p.IsAlive) { _projectiles.RemoveAt(i); continue; }

            switch (p.Type)
            {
                case ProjectileType.Bullet: AdvanceBullet(p, terrain, tankList); break;
                case ProjectileType.Shrapnel: AdvanceShrapnel(p, terrain); break;
                case ProjectileType.ConcreteFoam:
                    AdvanceFoam(p, terrain, TerrainPixel.ConcreteHigh, TerrainPixel.ConcreteLow, skipConcrete: true);
                    break;
                case ProjectileType.DirtFoam:
                    AdvanceFoam(p, terrain, TerrainPixel.DirtHigh, TerrainPixel.DirtLow, skipConcrete: false);
                    break;
            }
        }

        if (_projectiles.Count > Tweaks.Perf.ProjectileCompactThreshold)
            _projectiles.RemoveAll(p => !p.IsAlive);
    }

    private void AdvanceBullet(Projectile p, Terrain terrain, TankList? tankList)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(p.Speed.Length));
        var stepDir = p.Speed * (1f / steps);

        for (int s = 0; s < steps; s++)
        {
            p.Position += stepDir;
            var ipos = (Position)p.Position;

            if (!terrain.IsInside(ipos)) { p.IsAlive = false; return; }

            var pix = terrain.GetPixelRaw(ipos);

            if (tankList != null)
            {
                var hitTank = tankList.CheckTankCollision(ipos, p.OwnerColor);
                if (hitTank != null)
                {
                    hitTank.Reactor.Exhaust(new ReactorState(0, Tweaks.Weapon.BulletDamage));
                    SpawnNormalExplosion(ipos);
                    p.IsAlive = false;
                    return;
                }
            }

            if (Pixel.IsAnyCollision(pix))
            {
                SpawnNormalExplosion(ipos);
                p.IsAlive = false;
                return;
            }
        }
    }

    private void SpawnNormalExplosion(Position pos)
    {
        AddRange(ExplosionFactory.CreateExplosion(pos,
            Tweaks.Explosion.Normal.ShrapnelCount,
            Tweaks.Explosion.Normal.Speed,
            Tweaks.Explosion.Normal.Frames));
    }

    private void AdvanceShrapnel(Projectile p, Terrain terrain)
    {
        if (p.Life-- <= 0) { p.IsAlive = false; return; }

        p.Position += p.Speed;
        var ipos = (Position)p.Position;
        if (!terrain.IsInside(ipos)) { p.IsAlive = false; return; }

        var pix = terrain.GetPixelRaw(ipos);
        if (Pixel.IsBlockingCollision(pix))
        {
            var rng = Random.Shared;
            if ((Pixel.IsConcrete(pix) && rng.Next(1000) < Tweaks.Explosion.ChanceToDestroyConcrete) ||
                (Pixel.IsRock(pix) && rng.Next(1000) < Tweaks.Explosion.ChanceToDestroyRock))
            {
                terrain.SetPixel(ipos, rng.Next(2) == 0 ? TerrainPixel.DecalHigh : TerrainPixel.DecalLow);
            }
            p.IsAlive = false;
            return;
        }

        terrain.SetPixel(ipos, Random.Shared.Next(2) == 0 ? TerrainPixel.DecalHigh : TerrainPixel.DecalLow);
    }

    private static void AdvanceFoam(Projectile p, Terrain terrain,
        TerrainPixel highPixel, TerrainPixel lowPixel, bool skipConcrete)
    {
        if (p.Life-- <= 0) { p.IsAlive = false; return; }

        var prevPos = (Position)p.Position;
        p.Position += p.Speed;
        var ipos = (Position)p.Position;
        if (!terrain.IsInside(ipos)) { p.IsAlive = false; return; }

        var pix = terrain.GetPixelRaw(ipos);
        if (Pixel.IsAnyCollision(pix) && !(skipConcrete && Pixel.IsConcrete(pix)))
        {
            if (terrain.IsInside(prevPos))
                terrain.SetPixel(prevPos, Random.Shared.Next(2) == 0 ? highPixel : lowPixel);
            p.IsAlive = false;
        }
    }

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        var fireHot = new Color(0xff, 0x34, 0x08).ToArgb();
        var fireCold = new Color(0xba, 0x00, 0x00).ToArgb();
        var concreteColor = new Color(0xba, 0xba, 0xcc).ToArgb();
        var dirtColor = new Color(0xaa, 0x50, 0x03).ToArgb();

        foreach (var p in _projectiles)
        {
            if (!p.IsAlive) continue;
            var ipos = (Position)p.Position;
            if (ipos.X < 0 || ipos.Y < 0 || ipos.X >= surfaceWidth || ipos.Y >= surfaceHeight) continue;

            uint color = p.Type switch
            {
                ProjectileType.Bullet => fireHot,
                ProjectileType.Shrapnel => fireHot,
                ProjectileType.ConcreteFoam => concreteColor,
                ProjectileType.DirtFoam => dirtColor,
                _ => fireHot,
            };

            surface[ipos.X + ipos.Y * surfaceWidth] = color;
        }
    }
}
