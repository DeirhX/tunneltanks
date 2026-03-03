namespace Tunnerer.Core.Thermal;

using Tunnerer.Core.Config;
using Tunnerer.Core.Terrain;

/// <summary>
/// Applies one material-heat-exchange simulation step over a terrain heat field.
/// This is kept separate from TerrainGrid to make thermal behavior easier to test in isolation.
/// </summary>
public sealed class TerrainHeatEngine
{
    private readonly SimulationSettings _settings;
    private float[]? _delta;

    public TerrainHeatEngine()
        : this(SimulationSettings.FromTweaks())
    {
    }

    public TerrainHeatEngine(SimulationSettings settings)
    {
        _settings = settings;
    }

    public bool AddEnergyAt(float[] temperature, int width, int height, int x, int y, float amount)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            return false;
        int idx = x + y * width;
        return AddEnergyAt(temperature, idx, amount);
    }

    public bool AddEnergyAt(float[] temperature, int index, float amount)
    {
        if ((uint)index >= (uint)temperature.Length || amount == 0f)
            return false;

        temperature[index] += amount;
        return true;
    }

    public float GetTemperatureAt(float[] temperature, int len, int index)
    {
        if ((uint)index >= (uint)len)
            return 0f;
        return temperature[index];
    }

    public void Step(float[] temperature, TerrainPixel[] pixels, int width, int height)
        => Step(temperature, pixels, width, height, includeAmbientExchange: _settings.EnableThermalAmbientExchange);

    public void Step(
        float[] temperature,
        TerrainPixel[] pixels,
        int width,
        int height,
        bool includeAmbientExchange)
    {
        int len = width * height;
        if (temperature.Length < len)
            throw new ArgumentException("temperature length must cover simulation grid", nameof(temperature));

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
                float t1 = temperature[idx];

                if (x + 1 < width)
                    ExchangePair(idx, idx + 1, m1, t1, pixels, temperature);
                if (y + 1 < height)
                    ExchangePair(idx, idx + width, m1, t1, pixels, temperature);
                if (includeAmbientExchange)
                    ExchangeAmbient(idx, m1, t1);
            }
        }

        for (int i = 0; i < len; i++)
        {
            temperature[i] += _delta[i];
        }

        ApplyFixedTemperatureCells(temperature, pixels, len);
    }

    public double SumInternalEnergy(float[] temperature, TerrainPixel[] pixels)
    {
        if (pixels.Length < temperature.Length)
            throw new ArgumentException("pixels length must cover internal state", nameof(pixels));

        double sum = 0.0;
        for (int i = 0; i < temperature.Length; i++)
        {
            ThermalMaterial m = Pixel.GetThermalMaterial(pixels[i]);
            float c = GetHeatCapacity(m);
            sum += temperature[i] * c;
        }

        return sum;
    }

    private void ExchangePair(int idxA, int idxB, ThermalMaterial mA, float tA, TerrainPixel[] pixels, float[] temperature)
    {
        ThermalMaterial mB = Pixel.GetThermalMaterial(pixels[idxB]);
        float tB = temperature[idxB];
        float delta = tA - tB;
        if (MathF.Abs(delta) < 0.0001f) return;

        float cA = GetHeatCapacity(mA);
        float cB = GetHeatCapacity(mB);
        float k = GetConductance(mA, mB);
        float dQ = k * delta * Tweaks.World.ThermalDt;

        // Keep explicit pair exchange numerically stable under large deltas.
        // Pairwise cap is conservative because each cell can exchange with multiple
        // neighbors in one step; this avoids multi-neighbor overshoot that can
        // create heat after temperature floor clamping.
        float maxStableDQ = MathF.Min(cA, cB) * MathF.Abs(delta) * 0.125f;
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

    private void ApplyFixedTemperatureCells(float[] temperature, TerrainPixel[] pixels, int len)
    {
        for (int i = 0; i < len; i++)
        {
            ThermalMaterial material = Pixel.GetThermalMaterial(pixels[i]);
            if (!IsFixedTemperatureMaterial(material))
                continue;
            float target = GetFixedTemperature(material);
            temperature[i] = target;
        }
    }

    private static float GetHeatCapacity(ThermalMaterial material) => material switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalCapacityAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalCapacityDirt,
        ThermalMaterial.Stone => Tweaks.World.ThermalCapacityStone,
        _ => Tweaks.World.ThermalCapacityBase,
    };

    private static float GetConductance(ThermalMaterial a, ThermalMaterial b)
    {
        if (a > b) (a, b) = (b, a);
        return (a, b) switch
        {
            (ThermalMaterial.Air, ThermalMaterial.Air) => Tweaks.World.ThermalKAirAir,
            (ThermalMaterial.Air, ThermalMaterial.Dirt) => Tweaks.World.ThermalKAirDirt,
            (ThermalMaterial.Air, ThermalMaterial.Stone) => Tweaks.World.ThermalKAirStone,
            (ThermalMaterial.Air, ThermalMaterial.Base) => Tweaks.World.ThermalKAirBase,
            (ThermalMaterial.Dirt, ThermalMaterial.Dirt) => Tweaks.World.ThermalKDirtDirt,
            (ThermalMaterial.Dirt, ThermalMaterial.Stone) => Tweaks.World.ThermalKDirtStone,
            (ThermalMaterial.Dirt, ThermalMaterial.Base) => Tweaks.World.ThermalKDirtBase,
            (ThermalMaterial.Stone, ThermalMaterial.Stone) => Tweaks.World.ThermalKStoneStone,
            (ThermalMaterial.Stone, ThermalMaterial.Base) => Tweaks.World.ThermalKStoneBase,
            _ => Tweaks.World.ThermalKBaseBase,
        };
    }

    private float GetAmbientConductance(ThermalMaterial material) => material switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalKAmbientAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalKAmbientDirt,
        ThermalMaterial.Stone => _settings.EnableStoneAmbientExchange ? Tweaks.World.ThermalKAmbientStone : 0f,
        _ => Tweaks.World.ThermalKAmbientBase,
    };

    private static bool IsFixedTemperatureMaterial(ThermalMaterial material)
        => material == ThermalMaterial.Base;

    private static float GetFixedTemperature(ThermalMaterial material)
        => material == ThermalMaterial.Base ? Tweaks.World.ThermalFixedBaseTemperature : 0f;
}
