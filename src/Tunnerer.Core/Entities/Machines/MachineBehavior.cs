namespace Tunnerer.Core.Entities.Machines;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;
using Tunnerer.Core.Resources;
using Tunnerer.Core.Terrain;

/// <summary>
/// Defines per-type machine behavior in one place.
/// Adding a new machine type = one entry in <see cref="MachineBehaviors.Get"/>.
/// </summary>
public record MachineBehavior(
    TimeSpan ActionInterval,
    Action<Machine, TerrainGrid>? PerformAction,
    Color ActiveColor,
    MaterialAmount BuildCost);

public static class MachineBehaviors
{
    public static readonly MachineBehavior Harvester = new(
        TimeSpan.FromMilliseconds(Tweaks.Machine.HarvesterIntervalMs),
        HarvestNearby,
        Tweaks.Colors.Harvester,
        new MaterialAmount(Tweaks.Machine.HarvesterDirtCost, 0));

    public static readonly MachineBehavior Charger = new(
        TimeSpan.FromMilliseconds(Tweaks.Machine.ChargerIntervalMs),
        null,
        Tweaks.Colors.Charger,
        new MaterialAmount(Tweaks.Machine.ChargerDirtCost, 0));

    public static MachineBehavior Get(MachineType type) => type switch
    {
        MachineType.Harvester => Harvester,
        MachineType.Charger => Charger,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static void HarvestNearby(Machine machine, TerrainGrid terrain)
    {
        int range = Tweaks.Machine.HarvestRange;
        var center = machine.Position;
        for (int dy = -range; dy <= range; dy++)
            for (int dx = -range; dx <= range; dx++)
            {
                if (dx * dx + dy * dy > range * range) continue;
                var pos = new Position(center.X + dx, center.Y + dy);
                if (!terrain.IsInside(pos)) continue;

                if (Pixel.IsDirt(terrain.GetPixelRaw(pos)))
                {
                    terrain.SetPixel(pos, TerrainPixel.Blank);
                    return;
                }
            }
    }
}
