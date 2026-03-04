namespace Tunnerer.Core.Entities;

using Tunnerer.Core.Config;

internal static class TankHeatModel
{
    public static float ComputeActionHeat(int frameDugPixels, bool frameShotFired)
    {
        float heat = frameDugPixels * Tweaks.Tank.HeatDigPerPixel;
        if (frameShotFired)
            heat += Tweaks.Tank.HeatShootPerShot;
        return heat;
    }

    public static float ComputeTankTerrainHeatFlow(float tankTemperature, float terrainTemperature)
    {
        float deltaT = terrainTemperature - tankTemperature;
        return Tweaks.Tank.TankTerrainConductance * deltaT;
    }

    public static float ComputeTankDeltaFromHeatFlow(float dQ)
    {
        return Tweaks.Tank.TankHeatCapacity > 0f ? dQ / Tweaks.Tank.TankHeatCapacity : 0f;
    }

    public static float ComputeTerrainDeltaFromHeatFlow(float dQ)
    {
        return Tweaks.Tank.TerrainHeatCapacity > 0f ? -dQ / Tweaks.Tank.TerrainHeatCapacity : 0f;
    }

    public static float ComputeTankAmbientExchange(float tankTemperature, bool atOwnBase)
    {
        float ambient = atOwnBase ? 0f : Tweaks.Tank.HeatAmbientOutsideBase;
        float conductance = atOwnBase ? Tweaks.Tank.TankBaseConductance : Tweaks.Tank.TankAmbientConductance;
        float dQ = conductance * (ambient - tankTemperature);
        return Tweaks.Tank.TankHeatCapacity > 0f ? dQ / Tweaks.Tank.TankHeatCapacity : 0f;
    }

    public static int ComputeOverheatDamage(float heat)
    {
        if (heat <= Tweaks.Tank.HeatSafeMax) return 0;
        float excess = heat - Tweaks.Tank.HeatSafeMax;
        return (int)(excess * Tweaks.Tank.OverheatDamagePerDegree);
    }

    public static float ClampHeat(float heat)
    {
        return Math.Max(0f, heat);
    }
}
