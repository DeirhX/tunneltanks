namespace Tunnerer.Core.Thermal;

using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;

/// <summary>
/// Computes a full tank heat step against terrain + ambient.
/// Extracted from Tank to make thermal behavior testable with real-world wiring.
/// </summary>
public sealed class TankHeatEngine
{
    public float Advance(
        float currentHeat,
        int frameDugPixels,
        bool frameShotFired,
        bool atOwnBase,
        float sampledTerrainTemperature,
        float sampledAirTemperature,
        int sampleCells,
        Func<int, int> applyTerrainTotalDelta,
        Func<int, int> applyAirTotalDelta,
        out int overheatDamage)
    {
        float nextHeat = currentHeat;
        nextHeat += TankHeatModel.ComputeActionHeat(frameDugPixels, frameShotFired);

        float dQTerrain = TankHeatModel.ComputeTankTerrainHeatFlow(nextHeat, sampledTerrainTemperature);

        if (sampleCells > 0 && Tweaks.Tank.TerrainHeatCapacity > 0f)
        {
            float desiredTerrainAvgDelta = TankHeatModel.ComputeTerrainDeltaFromHeatFlow(dQTerrain);
            int desiredTerrainTotalDelta = (int)MathF.Round(desiredTerrainAvgDelta * sampleCells);
            int appliedTerrainTotalDelta = applyTerrainTotalDelta(desiredTerrainTotalDelta);
            float appliedTerrainAvgDelta = appliedTerrainTotalDelta / (float)sampleCells;
            float appliedDQTerrain = -appliedTerrainAvgDelta * Tweaks.Tank.TerrainHeatCapacity;
            nextHeat += TankHeatModel.ComputeTankDeltaFromHeatFlow(appliedDQTerrain);
        }
        else
        {
            nextHeat += TankHeatModel.ComputeTankDeltaFromHeatFlow(dQTerrain);
        }

        float dQAir = TankHeatModel.ComputeTankAirHeatFlow(nextHeat, sampledAirTemperature, atOwnBase);
        if (sampleCells > 0 && Tweaks.World.ThermalCapacityAir > 0f)
        {
            float desiredAirAvgDelta = -dQAir / Tweaks.World.ThermalCapacityAir;
            int desiredAirTotalDelta = (int)MathF.Round(desiredAirAvgDelta * sampleCells);
            int appliedAirTotalDelta = applyAirTotalDelta(desiredAirTotalDelta);
            float appliedAirAvgDelta = appliedAirTotalDelta / (float)sampleCells;
            float appliedDQAir = -appliedAirAvgDelta * Tweaks.World.ThermalCapacityAir;
            nextHeat += TankHeatModel.ComputeTankDeltaFromHeatFlow(appliedDQAir);
        }
        else
        {
            nextHeat += TankHeatModel.ComputeTankDeltaFromHeatFlow(dQAir);
        }

        overheatDamage = TankHeatModel.ComputeOverheatDamage(nextHeat);
        return TankHeatModel.ClampHeat(nextHeat);
    }
}
