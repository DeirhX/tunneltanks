namespace Tunnerer.Core.Config;

public readonly record struct SimulationSettings(
    bool EnableThermalAmbientExchange,
    bool EnableStoneAmbientExchange,
    bool EnableConservativeAirField,
    int ThermalActiveTileSize,
    float ThermalSparseFallbackCoverage,
    int ThermalParallelRegionThreshold,
    int ThermalMaxWorkers,
    float ThermalActiveTemperatureThreshold,
    float ThermalAirCellCoupling,
    float ThermalAirNeighborCoupling)
{
    public static SimulationSettings FromTweaks()
        => new(
            EnableThermalAmbientExchange: Tweaks.World.EnableThermalAmbientExchange,
            EnableStoneAmbientExchange: Tweaks.World.EnableStoneAmbientExchange,
            EnableConservativeAirField: Tweaks.World.EnableConservativeAirField,
            ThermalActiveTileSize: Tweaks.World.ThermalActiveTileSize,
            ThermalSparseFallbackCoverage: Tweaks.World.ThermalSparseFallbackCoverage,
            ThermalParallelRegionThreshold: Tweaks.World.ThermalParallelRegionThreshold,
            ThermalMaxWorkers: Tweaks.World.ThermalMaxWorkers,
            ThermalActiveTemperatureThreshold: Tweaks.World.ThermalActiveTemperatureThreshold,
            ThermalAirCellCoupling: Tweaks.World.ThermalAirCellCoupling,
            ThermalAirNeighborCoupling: Tweaks.World.ThermalAirNeighborCoupling);
}
