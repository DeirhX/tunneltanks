namespace Tunnerer.Core.Entities;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;
using Tunnerer.Core.Resources;
using Tunnerer.Core.Terrain;

public class TankBase
{
    public Position Position { get; }
    public int Color { get; }
    public BoundingBox BoundingBox { get; } = new(Tweaks.Base.BaseSize / 2, Tweaks.Base.BaseSize / 2);
    public Reactor Reactor { get; } = new(
        initial: new ReactorState(Tweaks.Base.InitialHeat, Tweaks.Base.InitialHealth),
        capacity: new ReactorState(Tweaks.Base.HeatCapacity, Tweaks.Base.HealthCapacity));
    public MaterialContainer Materials { get; } = new(
        initial: new MaterialAmount(0, 0),
        capacity: new MaterialAmount(Tweaks.Base.MaterialDirtCapacity, Tweaks.Base.MaterialMineralsCapacity));

    private static int Half => Tweaks.Base.BaseSize / 2;
    private static int DoorHalf => Tweaks.Base.DoorSize / 2;

    public TankBase(Position position, int color)
    {
        Position = position;
        Color = color;
    }

    public bool IsInside(Position tested) => BoundingBox.IsInside(tested, Position);

    public void Advance()
    {
        Reactor.Exhaust(new ReactorState(Tweaks.Base.HeatCooldown, 0));
        Reactor.Add(new ReactorState(0, Tweaks.Base.HealthRegen));
    }

    public void RechargeTank(Reactor tankReactor, int tankColor)
    {
        if (tankColor == Color)
            GiveReactorResources(tankReactor, heatCooldown: Tweaks.Base.HomeCooldownHeat, healthRegen: Tweaks.Base.HomeRechargeHealth);
        else
            GiveReactorResources(tankReactor, heatCooldown: Tweaks.Base.ForeignCooldownHeat, healthRegen: Tweaks.Base.ForeignRechargeHealth);
    }

    private void GiveReactorResources(Reactor target, int heatCooldown, int healthRegen)
    {
        // "Recharge" for heat means cooling: reduce target heat while healing health.
        target.Exhaust(new ReactorState(heatCooldown, 0));
        target.Add(new ReactorState(0, healthRegen));
    }

    public void AbsorbResources(MaterialContainer other)
    {
        Materials.Absorb(other);
    }

    public void AbsorbResources(MaterialContainer other, MaterialAmount rate)
    {
        var absorber = new MaterialContainer(initial: default, capacity: rate);
        absorber.Absorb(other);
        Materials.Absorb(absorber);
        other.Absorb(absorber);
    }

    public void Draw(Surface surface)
    {
        var outlineColor = Tweaks.Colors.BaseOutline.ToArgb();

        ForEachEdgePixel(Position, (px, py, isDoor) =>
        {
            if (!surface.IsInside(px, py)) return;
            if (!isDoor)
                surface.Pixels[px + py * surface.Width] = outlineColor;
        });
    }

    public void CreateInTerrain(TerrainGrid terrain, int color)
    {
        int half = Half;

        for (int y = -half; y <= half; y++)
            for (int x = -half; x <= half; x++)
            {
                var pix = Position + new Offset(x, y);
                if (!terrain.IsInside(pix)) continue;

                if (IsEdge(x, y))
                {
                    if (IsDoor(x))
                        terrain.SetPixel(pix, TerrainPixel.BaseBarrier);
                    else
                        terrain.SetPixel(pix, (TerrainPixel)((byte)TerrainPixel.BaseMin + color));
                }
                else
                {
                    terrain.SetPixel(pix, TerrainPixel.Blank);
                }
            }
    }

    private static bool IsEdge(int dx, int dy) => Math.Abs(dx) == Half || Math.Abs(dy) == Half;
    private static bool IsDoor(int dx) => dx >= -DoorHalf && dx <= DoorHalf;

    /// <summary>
    /// Iterates edge pixels of the base outline, calling visitor(px, py, isDoor).
    /// </summary>
    private static void ForEachEdgePixel(Position center, Action<int, int, bool> visitor)
    {
        int half = Half;
        for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++)
            {
                if (!IsEdge(dx, dy)) continue;
                visitor(center.X + dx, center.Y + dy, IsDoor(dx));
            }
    }
}
