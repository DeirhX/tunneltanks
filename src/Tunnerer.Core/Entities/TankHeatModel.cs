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
        float dQ = Tweaks.Tank.TankTerrainConductance * deltaT;
        float maxStableDQ = Tweaks.Tank.TankHeatCapacity * MathF.Abs(deltaT) * 0.01f;
        if (dQ > maxStableDQ) dQ = maxStableDQ;
        else if (dQ < -maxStableDQ) dQ = -maxStableDQ;
        return dQ;
    }

    public static float ComputeTankDeltaFromHeatFlow(float dQ)
    {
        return Tweaks.Tank.TankHeatCapacity > 0f ? dQ / Tweaks.Tank.TankHeatCapacity : 0f;
    }

    public static float ComputeTerrainDeltaFromHeatFlow(float dQ)
    {
        return Tweaks.Tank.TerrainHeatCapacity > 0f ? -dQ / Tweaks.Tank.TerrainHeatCapacity : 0f;
    }

    public static float ComputeTankAirHeatFlow(float tankTemperature, float airTemperature, bool atOwnBase)
    {
        float conductance = atOwnBase ? Tweaks.Tank.TankBaseConductance : Tweaks.Tank.TankAmbientConductance;
        float deltaT = airTemperature - tankTemperature;
        float dQ = conductance * deltaT;
        float maxStableDQ = Tweaks.Tank.TankHeatCapacity * MathF.Abs(deltaT) * 0.01f;
        if (dQ > maxStableDQ) dQ = maxStableDQ;
        else if (dQ < -maxStableDQ) dQ = -maxStableDQ;
        return dQ;
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
