namespace TunnelTanks.Core.Entities.Projectiles;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Entities;
using TunnelTanks.Core.Resources;
using TunnelTanks.Core.Collision;

/// <summary>
/// Defines per-type projectile behavior: how it advances and what color it draws.
/// Adding a new projectile type = one entry in <see cref="ProjectileList.Behaviors"/>.
/// </summary>
public record ProjectileBehavior(
    Action<Projectile, CollisionSolver, ProjectileList> Advance,
    Color DrawColor);

public class ProjectileList
{
    private readonly List<Projectile> _projectiles = new();
    private readonly List<Projectile> _pending = new();

    /// <summary>
    /// Single source of truth for per-type projectile behavior.
    /// To add a new projectile type: add the enum value, then add one entry here.
    /// </summary>
    public static readonly ProjectileBehavior[] Behaviors = BuildBehaviors();

    private static ProjectileBehavior[] BuildBehaviors()
    {
        var values = Enum.GetValues<ProjectileType>();
        int max = 0;
        foreach (var v in values)
            max = Math.Max(max, (int)v);

        var behaviors = new ProjectileBehavior[max + 1];
        behaviors[(int)ProjectileType.Bullet] = new(AdvanceBullet, Tweaks.Colors.FireHot);
        behaviors[(int)ProjectileType.Shrapnel] = new(AdvanceShrapnel, Tweaks.Colors.FireHot);
        behaviors[(int)ProjectileType.ConcreteFoam] = new(
            (p, solver, self) => AdvanceFoam(p, solver.Terrain,
                TerrainPixel.ConcreteHigh, TerrainPixel.ConcreteLow, skipConcrete: true),
            Tweaks.Colors.Concrete);
        behaviors[(int)ProjectileType.DirtFoam] = new(
            (p, solver, self) => AdvanceFoam(p, solver.Terrain,
                TerrainPixel.DirtHigh, TerrainPixel.DirtLow, skipConcrete: false),
            Tweaks.Colors.DirtProjectile);
        return behaviors;
    }

    public int Count => _projectiles.Count + _pending.Count;
    public void Add(Projectile p) => _pending.Add(p);
    public void AddRange(IEnumerable<Projectile> items) => _pending.AddRange(items);

    public void RemoveAll()
    {
        _projectiles.Clear();
        _pending.Clear();
    }

    public void Advance(CollisionSolver solver)
    {
        _projectiles.AddRange(_pending);
        _pending.Clear();

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            if (!p.IsAlive) { _projectiles.RemoveAt(i); continue; }

            Behaviors[(int)p.Type].Advance(p, solver, this);
        }

        if (_projectiles.Count > Tweaks.Perf.ProjectileCompactThreshold)
            _projectiles.RemoveAll(p => !p.IsAlive);
    }

    private static void AdvanceBullet(Projectile p, CollisionSolver solver, ProjectileList self)
    {
        var terrain = solver.Terrain;
        int steps = Math.Max(1, (int)MathF.Ceiling(p.Speed.Length));
        var stepDir = p.Speed * (1f / steps);

        for (int s = 0; s < steps; s++)
        {
            p.Position += stepDir;
            var ipos = (Position)p.Position;

            if (!terrain.IsInside(ipos)) { p.IsAlive = false; return; }

            bool hit = solver.TestPoint(ipos,
                onTank: tank =>
                {
                    if (tank.Color == p.OwnerColor) return false;
                    tank.Reactor.Exhaust(new ReactorState(0, Tweaks.Weapon.BulletDamage));
                    return true;
                },
                onTerrain: pix => Pixel.IsAnyCollision(pix));

            if (hit)
            {
                self.SpawnNormalExplosion(ipos);
                p.IsAlive = false;
                return;
            }
        }
    }

    private void SpawnNormalExplosion(Position pos)
    {
        AddRange(ExplosionFactory.CreateExplosion(pos, Tweaks.Explosion.Normal));
    }

    private static void AdvanceShrapnel(Projectile p, CollisionSolver solver, ProjectileList self)
    {
        var terrain = solver.Terrain;
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

    private static void AdvanceFoam(Projectile p, TerrainGrid terrain,
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

    public void Draw(Surface surface)
    {
        foreach (var p in _projectiles)
        {
            if (!p.IsAlive) continue;
            var ipos = (Position)p.Position;
            if (!surface.IsInside(ipos.X, ipos.Y)) continue;

            surface.Pixels[ipos.X + ipos.Y * surface.Width] = Behaviors[(int)p.Type].DrawColor.ToArgb();
        }
    }
}
