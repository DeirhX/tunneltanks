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

    public static float ComputeTerrainAbsorb(float terrainHeatNormalized)
    {
        return terrainHeatNormalized * Tweaks.Tank.HeatTerrainAbsorb * Tweaks.Tank.HeatMax;
    }

    public static float ComputeCooling(bool atOwnBase)
    {
        float cooling = Tweaks.Tank.HeatCoolPerFrame;
        if (atOwnBase)
            cooling += Tweaks.Tank.HeatBaseCoolBonus;
        return cooling;
    }

    public static int ComputeOverheatDamage(float heat)
    {
        if (heat <= Tweaks.Tank.HeatOverheatThreshold) return 0;
        float excess = heat - Tweaks.Tank.HeatOverheatThreshold;
        return (int)(excess * Tweaks.Tank.HeatDamagePerFrame);
    }

    public static float ClampHeat(float heat)
    {
        return Math.Clamp(heat, 0f, Tweaks.Tank.HeatMax);
    }
}
