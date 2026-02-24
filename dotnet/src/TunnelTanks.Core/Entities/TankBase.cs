namespace TunnelTanks.Core.Entities;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Resources;
using TunnelTanks.Core.Terrain;

public class TankBase
{
    public Position Position { get; }
    public int Color { get; }
    public BoundingBox BoundingBox { get; } = new(Tweaks.Base.BaseSize / 2, Tweaks.Base.BaseSize / 2);
    public Reactor Reactor { get; } = new(Tweaks.Base.InitialEnergy, Tweaks.Base.InitialHealth,
        Tweaks.Base.EnergyCapacity, Tweaks.Base.HealthCapacity);
    public MaterialContainer Materials { get; } = new(0, 0,
        Tweaks.Base.MaterialDirtCapacity, Tweaks.Base.MaterialMineralsCapacity);

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
        Reactor.Add(new ReactorState(Tweaks.Base.EnergyRegen, Tweaks.Base.HealthRegen));
    }

    public void RechargeTank(Reactor tankReactor, int tankColor)
    {
        if (tankColor == Color)
            GiveReactorResources(tankReactor, new ReactorState(Tweaks.Base.HomeRechargeEnergy, Tweaks.Base.HomeRechargeHealth));
        else
            GiveReactorResources(tankReactor, new ReactorState(Tweaks.Base.ForeignRechargeEnergy, Tweaks.Base.ForeignRechargeHealth));
    }

    private void GiveReactorResources(Reactor target, ReactorState rate)
    {
        var absorber = new Reactor(0, 0, rate.Energy, rate.Health);
        absorber.Absorb(Reactor);
        target.Absorb(absorber);
        Reactor.Absorb(absorber);
    }

    public void AbsorbResources(MaterialContainer other)
    {
        Materials.Absorb(other);
    }

    public void AbsorbResources(MaterialContainer other, MaterialAmount rate)
    {
        var absorber = new MaterialContainer(0, 0, rate.Dirt, rate.Minerals);
        absorber.Absorb(other);
        Materials.Absorb(absorber);
        other.Absorb(absorber);
    }

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        var outlineColor = Tweaks.Colors.BaseOutline.ToArgb();

        ForEachEdgePixel(Position, (px, py, isDoor) =>
        {
            if (px < 0 || py < 0 || px >= surfaceWidth || py >= surfaceHeight) return;
            if (!isDoor)
                surface[px + py * surfaceWidth] = outlineColor;
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
