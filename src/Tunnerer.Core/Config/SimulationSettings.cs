namespace Tunnerer.Core.Config;

public readonly record struct SimulationSettings(
    bool EnableMaterialHeatExchange,
    bool EnableThermalAmbientExchange,
    bool EnableStoneAmbientExchange,
    int ThermalActiveTileSize,
    float ThermalSparseFallbackCoverage,
    int ThermalParallelRegionThreshold,
    int ThermalMaxWorkers,
    float ThermalActiveTemperatureThreshold)
{
    public static SimulationSettings FromTweaks()
        => new(
            EnableMaterialHeatExchange: Tweaks.World.EnableMaterialHeatExchange,
            EnableThermalAmbientExchange: Tweaks.World.EnableThermalAmbientExchange,
            EnableStoneAmbientExchange: Tweaks.World.EnableStoneAmbientExchange,
            ThermalActiveTileSize: Tweaks.World.ThermalActiveTileSize,
            ThermalSparseFallbackCoverage: Tweaks.World.ThermalSparseFallbackCoverage,
            ThermalParallelRegionThreshold: Tweaks.World.ThermalParallelRegionThreshold,
            ThermalMaxWorkers: Tweaks.World.ThermalMaxWorkers,
            ThermalActiveTemperatureThreshold: Tweaks.World.ThermalActiveTemperatureThreshold);
}
