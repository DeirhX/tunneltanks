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
        int sampleCells,
        Func<int, int> applyTerrainTotalDelta,
        out int overheatDamage)
    {
        float nextHeat = currentHeat;
        nextHeat += TankHeatModel.ComputeActionHeat(frameDugPixels, frameShotFired);

        bool includeAmbientExchange = Tweaks.World.EnableThermalAmbientExchange;
        float terrainTemperature = sampledTerrainTemperature;
        if (includeAmbientExchange)
        {
            float ambientBaseline = atOwnBase ? 0f : Tweaks.Tank.HeatAmbientOutsideBase;
            terrainTemperature = MathF.Max(ambientBaseline, sampledTerrainTemperature);
        }
        float dQTerrain = atOwnBase
            ? 0f
            : TankHeatModel.ComputeTankTerrainHeatFlow(nextHeat, terrainTemperature);

        if (!atOwnBase && sampleCells > 0 && Tweaks.Tank.TerrainHeatCapacity > 0f)
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

        if (includeAmbientExchange)
            nextHeat += TankHeatModel.ComputeTankAmbientExchange(nextHeat, atOwnBase);
        overheatDamage = TankHeatModel.ComputeOverheatDamage(nextHeat);
        return TankHeatModel.ClampHeat(nextHeat);
    }
}
