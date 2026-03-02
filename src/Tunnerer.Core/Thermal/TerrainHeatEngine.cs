namespace Tunnerer.Core.Thermal;

using Tunnerer.Core.Config;
using Tunnerer.Core.Terrain;

/// <summary>
/// Applies one material-heat-exchange simulation step over a terrain heat field.
/// This is kept separate from TerrainGrid to make thermal behavior easier to test in isolation.
/// </summary>
public sealed class TerrainHeatEngine
{
    private float[]? _delta;

    public void Step(byte[] heat, TerrainPixel[] pixels, int width, int height)
    {
        int len = width * height;
        if (_delta == null || _delta.Length < len)
            _delta = new float[len];
        Array.Clear(_delta, 0, len);

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int idx = row + x;
                ThermalMaterial m1 = Pixel.GetThermalMaterial(pixels[idx]);
                float t1 = heat[idx];

                if (x + 1 < width)
                    ExchangePair(idx, idx + 1, m1, t1, heat, pixels);
                if (y + 1 < height)
                    ExchangePair(idx, idx + width, m1, t1, heat, pixels);
                ExchangeAmbient(idx, m1, t1);
            }
        }

        for (int i = 0; i < len; i++)
        {
            if (_delta[i] == 0f) continue;
            int nextVal = (int)MathF.Round(heat[i] + _delta[i]);
            heat[i] = (byte)Math.Clamp(nextVal, 0, 255);
        }
    }

    private void ExchangePair(int idxA, int idxB, ThermalMaterial mA, float tA, byte[] heat, TerrainPixel[] pixels)
    {
        ThermalMaterial mB = Pixel.GetThermalMaterial(pixels[idxB]);
        float tB = heat[idxB];
        float delta = tA - tB;
        if (MathF.Abs(delta) < 0.0001f) return;

        float cA = GetHeatCapacity(mA);
        float cB = GetHeatCapacity(mB);
        float k = GetConductance(mA, mB);
        float dQ = k * delta * Tweaks.World.ThermalDt;

        // Keep explicit pair exchange numerically stable under large deltas.
        float maxStableDQ = MathF.Min(cA, cB) * MathF.Abs(delta) * 0.5f;
        if (dQ > maxStableDQ) dQ = maxStableDQ;
        else if (dQ < -maxStableDQ) dQ = -maxStableDQ;
        if (MathF.Abs(dQ) < 0.0001f) return;

        _delta![idxA] -= dQ / cA;
        _delta[idxB] += dQ / cB;
    }

    private void ExchangeAmbient(int idx, ThermalMaterial material, float temperature)
    {
        float ambient = Tweaks.World.ThermalAmbientTemperature;
        if (temperature <= ambient)
            return;

        float k = GetAmbientConductance(material);
        float dQ = k * (ambient - temperature) * Tweaks.World.ThermalDt;
        if (MathF.Abs(dQ) < 0.0001f) return;

        float capacity = GetHeatCapacity(material);
        _delta![idx] += dQ / capacity;
    }

    private static float GetHeatCapacity(ThermalMaterial material) => material switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalCapacityAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalCapacityDirt,
        _ => Tweaks.World.ThermalCapacityStone,
    };

    private static float GetConductance(ThermalMaterial a, ThermalMaterial b)
    {
        if (a > b) (a, b) = (b, a);
        return (a, b) switch
        {
            (ThermalMaterial.Air, ThermalMaterial.Air) => Tweaks.World.ThermalKAirAir,
            (ThermalMaterial.Air, ThermalMaterial.Dirt) => Tweaks.World.ThermalKAirDirt,
            (ThermalMaterial.Air, ThermalMaterial.Stone) => Tweaks.World.ThermalKAirStone,
            (ThermalMaterial.Dirt, ThermalMaterial.Dirt) => Tweaks.World.ThermalKDirtDirt,
            (ThermalMaterial.Dirt, ThermalMaterial.Stone) => Tweaks.World.ThermalKDirtStone,
            _ => Tweaks.World.ThermalKStoneStone,
        };
    }

    private static float GetAmbientConductance(ThermalMaterial material) => material switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalKAmbientAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalKAmbientDirt,
        _ => Tweaks.World.ThermalKAmbientStone,
    };
}
