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

    public void Advance(TerrainGrid terrain)
    {
        int outerRadius = Half;
        int midRadius = Math.Max(1, (outerRadius * 2) / 3);
        int innerRadius = Math.Max(1, outerRadius / 3);

        int pulledFromTerrain = 0;
        pulledFromTerrain -= terrain.AddHeatTotalInRadiusArea(Position, outerRadius, -Tweaks.Base.AreaCoolingOuterHeatTotal);
        pulledFromTerrain -= terrain.AddHeatTotalInRadiusArea(Position, midRadius, -Tweaks.Base.AreaCoolingMidHeatTotal);
        pulledFromTerrain -= terrain.AddHeatTotalInRadiusArea(Position, innerRadius, -Tweaks.Base.AreaCoolingInnerHeatTotal);

        int pulledFromAir = 0;
        pulledFromAir -= terrain.AddAirHeatTotalInRadiusArea(Position, outerRadius, -Tweaks.Base.AreaCoolingOuterAirTotal);
        pulledFromAir -= terrain.AddAirHeatTotalInRadiusArea(Position, midRadius, -Tweaks.Base.AreaCoolingMidAirTotal);
        pulledFromAir -= terrain.AddAirHeatTotalInRadiusArea(Position, innerRadius, -Tweaks.Base.AreaCoolingInnerAirTotal);

        int absorbedHeat = Math.Max(0, pulledFromTerrain + pulledFromAir);
        if (absorbedHeat > 0)
            Reactor.Add(new ReactorState(absorbedHeat, 0));

        int beforeHeat = Reactor.Heat;
        Reactor.Exhaust(new ReactorState(Tweaks.Base.HeatCooldown, 0));
        int releasedHeat = Math.Max(0, beforeHeat - (int)Reactor.Heat);
        if (releasedHeat > 0)
        {
            int toAir = (int)MathF.Round(releasedHeat * 0.8f);
            int toTerrain = releasedHeat - toAir;
            if (toAir != 0)
                terrain.AddAirHeatTotalInRadiusArea(Position, Half, toAir);
            if (toTerrain != 0)
                terrain.AddHeatTotalInRadiusArea(Position, Half, toTerrain);
        }

        // Keep base center as a hard thermal sink for gameplay/readability:
        // its cell should not retain residual terrain/air energy.
        float centerTerrainTemp = terrain.GetHeatTemperature(Position);
        if (centerTerrainTemp != 0f)
        {
            int centerTerrainDelta = -(int)MathF.Round(centerTerrainTemp);
            if (centerTerrainDelta != 0)
                terrain.AddHeatTotalInRadiusArea(Position, 0, centerTerrainDelta);
        }

        float centerAirTemp = terrain.GetAirTemperature(Position);
        if (centerAirTemp != 0f)
        {
            int centerAirDelta = -(int)MathF.Round(centerAirTemp);
            if (centerAirDelta != 0)
                terrain.AddAirHeatTotalInRadiusArea(Position, 0, centerAirDelta);
        }

        Reactor.Add(new ReactorState(0, Tweaks.Base.HealthRegen));
    }

    public void RechargeTank(Reactor tankReactor, int tankColor)
    {
        if (tankColor == Color)
            GiveReactorResources(tankReactor, Tweaks.Base.HomeRechargeHealth);
        else
            GiveReactorResources(tankReactor, Tweaks.Base.ForeignRechargeHealth);
    }

    private static void GiveReactorResources(Reactor target, int healthRegen)
    {
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
                    terrain.SetPixel(pix, TerrainPixel.BaseCore);
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
