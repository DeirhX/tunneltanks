namespace Tunnerer.Core.Config;

public readonly record struct SimulationSettings(
    bool EnableMaterialHeatExchange,
    bool EnableThermalAmbientExchange,
    bool EnableStoneAmbientExchange)
{
    public static SimulationSettings FromTweaks()
        => new(
            EnableMaterialHeatExchange: Tweaks.World.EnableMaterialHeatExchange,
            EnableThermalAmbientExchange: Tweaks.World.EnableThermalAmbientExchange,
            EnableStoneAmbientExchange: Tweaks.World.EnableStoneAmbientExchange);
}
