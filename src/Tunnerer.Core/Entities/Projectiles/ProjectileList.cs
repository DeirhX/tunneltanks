namespace Tunnerer.Core.Entities.Projectiles;

using Tunnerer.Core.Types;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Resources;
using Tunnerer.Core.Collision;
using Tunnerer.Core.Rendering;

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
    private FastRandom _rng;

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
            (p, solver, self) => AdvanceFoam(p, solver.Terrain, self,
                TerrainPixel.ConcreteHigh, TerrainPixel.ConcreteLow, skipConcrete: true),
            Tweaks.Colors.Concrete);
        behaviors[(int)ProjectileType.DirtFoam] = new(
            (p, solver, self) => AdvanceFoam(p, solver.Terrain, self,
                TerrainPixel.DirtHigh, TerrainPixel.DirtHigh, skipConcrete: false),
            Tweaks.Colors.DirtProjectile);
        return behaviors;
    }

    public ProjectileList(int? seed = null)
    {
        _rng = seed.HasValue
            ? new FastRandom(seed.Value)
            : new FastRandom((uint)Environment.TickCount);
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
            if (!TryAdvanceInside(terrain, p, stepDir, out var ipos))
                return;

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
                int bulletExplosionHeat = Math.Max(0, (int)MathF.Round(
                    Tweaks.Explosion.BulletHeatAmount * Tweaks.Explosion.BulletExplosionHeatScale));
                terrain.AddHeatRadius(ipos, bulletExplosionHeat, Tweaks.Explosion.BulletHeatRadius);
                p.IsAlive = false;
                return;
            }
        }
    }

    private void SpawnNormalExplosion(Position pos)
    {
        AddRange(ExplosionFactory.CreateExplosion(
            pos,
            Tweaks.Explosion.Normal,
            ref _rng,
            Tweaks.Explosion.BulletExplosionHeatScale));
    }

    public void AddExplosion(Position pos, ExplosionParams p, float shrapnelHeatScale = 1f)
    {
        AddRange(ExplosionFactory.CreateExplosion(pos, p, ref _rng, shrapnelHeatScale));
    }

    private static void AdvanceShrapnel(Projectile p, CollisionSolver solver, ProjectileList self)
    {
        var terrain = solver.Terrain;
        if (p.Life-- <= 0) { p.IsAlive = false; return; }

        if (!TryAdvanceInside(terrain, p, out var ipos))
            return;

        var pix = terrain.GetPixelRaw(ipos);
        if (Pixel.IsBlockingCollision(pix))
        {
            if ((Pixel.IsConcrete(pix) && self._rng.Chance1000(Tweaks.Explosion.ChanceToDestroyConcrete)) ||
                (Pixel.IsRock(pix) && self._rng.Chance1000(Tweaks.Explosion.ChanceToDestroyRock)))
            {
                terrain.SetPixel(ipos, TerrainPixel.DecalHigh);
            }
            int hitHeat = Math.Max(0, (int)MathF.Round(Tweaks.Explosion.ShrapnelHitHeat * p.HeatScale));
            terrain.AddHeat(ipos, hitHeat);
            p.IsAlive = false;
            return;
        }

        terrain.SetPixel(ipos, TerrainPixel.DecalHigh);
        int digHeat = Math.Max(0, (int)MathF.Round(Tweaks.Explosion.ShrapnelDigHeatAmount * p.HeatScale));
        terrain.AddHeatRadius(ipos, digHeat, Tweaks.Explosion.ShrapnelDigHeatRadius);
    }

    private static void AdvanceFoam(Projectile p, TerrainGrid terrain, ProjectileList self,
        TerrainPixel highPixel, TerrainPixel lowPixel, bool skipConcrete)
    {
        if (p.Life-- <= 0) { p.IsAlive = false; return; }

        var prevPos = (Position)p.Position;
        if (!TryAdvanceInside(terrain, p, out var ipos))
            return;

        var pix = terrain.GetPixelRaw(ipos);
        if (Pixel.IsAnyCollision(pix) && !(skipConcrete && Pixel.IsConcrete(pix)))
        {
            if (terrain.IsInside(prevPos))
                terrain.SetPixel(prevPos, self._rng.NextInt(2) == 0 ? highPixel : lowPixel);
            p.IsAlive = false;
        }
    }

    private static bool TryAdvanceInside(TerrainGrid terrain, Projectile p, out Position ipos)
    {
        p.Position += p.Speed;
        ipos = (Position)p.Position;
        if (!terrain.IsInside(ipos))
        {
            p.IsAlive = false;
            return false;
        }
        return true;
    }

    private static bool TryAdvanceInside(TerrainGrid terrain, Projectile p, VectorF step, out Position ipos)
    {
        p.Position += step;
        ipos = (Position)p.Position;
        if (!terrain.IsInside(ipos))
        {
            p.IsAlive = false;
            return false;
        }
        return true;
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

    public int CopyRenderStates(Span<ProjectileRenderState> destination)
    {
        int count = Math.Min(destination.Length, _projectiles.Count);
        for (int i = 0; i < count; i++)
        {
            var p = _projectiles[i];
            destination[i] = new ProjectileRenderState(
                Position: (Position)p.Position,
                Type: p.Type,
                IsAlive: p.IsAlive);
        }
        return count;
    }
}
