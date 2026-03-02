namespace Tunnerer.Core.Thermal;

using Tunnerer.Core.Config;
using Tunnerer.Core.Terrain;

/// <summary>
/// Applies one material-heat-exchange simulation step over a terrain heat field.
/// This is kept separate from TerrainGrid to make thermal behavior easier to test in isolation.
/// </summary>
public sealed class TerrainHeatEngine
{
    private float[]? _temperature;
    private float[]? _delta;
    private bool _stateDirty = true;

    public void MarkStateDirty() => _stateDirty = true;

    public void EnsureState(byte[] heat, int len) => EnsureStateFromHeat(heat, len);

    public bool AddEnergyAt(byte[] heat, int width, int height, int x, int y, int amount)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            return false;
        int idx = x + y * width;
        return AddEnergyAt(heat, idx, amount);
    }

    public bool AddEnergyAt(byte[] heat, int index, int amount)
    {
        if ((uint)index >= (uint)heat.Length || amount == 0)
            return false;

        EnsureStateFromHeat(heat, heat.Length);
        float next = Math.Clamp(_temperature![index] + amount, 0f, 255f);
        _temperature[index] = next;
        heat[index] = (byte)Math.Clamp((int)MathF.Round(next), 0, 255);
        return true;
    }

    public float GetTemperatureAt(byte[] heat, int len, int index)
    {
        EnsureStateFromHeat(heat, len);
        if ((uint)index >= (uint)len)
            return 0f;
        return _temperature![index];
    }

    public void Step(byte[] heat, TerrainPixel[] pixels, int width, int height)
        => Step(heat, pixels, width, height, includeAmbientExchange: Tweaks.World.EnableThermalAmbientExchange);

    public void Step(
        byte[] heat,
        TerrainPixel[] pixels,
        int width,
        int height,
        bool includeAmbientExchange)
    {
        int len = width * height;
        EnsureStateFromHeat(heat, len);

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
                float t1 = _temperature![idx];

                if (x + 1 < width)
                    ExchangePair(idx, idx + 1, m1, t1, pixels);
                if (y + 1 < height)
                    ExchangePair(idx, idx + width, m1, t1, pixels);
                if (includeAmbientExchange)
                    ExchangeAmbient(idx, m1, t1);
            }
        }

        for (int i = 0; i < len; i++)
        {
            float nextT = _temperature![i] + _delta[i];
            nextT = Math.Clamp(nextT, 0f, 255f);
            _temperature[i] = nextT;
            heat[i] = (byte)Math.Clamp((int)MathF.Round(nextT), 0, 255);
        }

        ApplyFixedTemperatureCells(heat, pixels, len);
    }

    public double SumInternalEnergy(TerrainPixel[] pixels)
    {
        if (_temperature == null)
            return 0.0;
        if (pixels.Length < _temperature.Length)
            throw new ArgumentException("pixels length must cover internal state", nameof(pixels));

        double sum = 0.0;
        for (int i = 0; i < _temperature.Length; i++)
        {
            ThermalMaterial m = Pixel.GetThermalMaterial(pixels[i]);
            float c = GetHeatCapacity(m);
            sum += _temperature[i] * c;
        }

        return sum;
    }

    private void ExchangePair(int idxA, int idxB, ThermalMaterial mA, float tA, TerrainPixel[] pixels)
    {
        ThermalMaterial mB = Pixel.GetThermalMaterial(pixels[idxB]);
        float tB = _temperature![idxB];
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

    private void ApplyFixedTemperatureCells(byte[] heat, TerrainPixel[] pixels, int len)
    {
        for (int i = 0; i < len; i++)
        {
            ThermalMaterial material = Pixel.GetThermalMaterial(pixels[i]);
            if (!IsFixedTemperatureMaterial(material))
                continue;
            float target = GetFixedTemperature(material);
            _temperature![i] = target;
            heat[i] = (byte)Math.Clamp((int)MathF.Round(target), 0, 255);
        }
    }

    private void EnsureStateFromHeat(byte[] heat, int len)
    {
        if (_temperature == null || _temperature.Length < len)
        {
            _temperature = new float[len];
            _stateDirty = true;
        }

        if (!_stateDirty)
            return;

        for (int i = 0; i < len; i++)
            _temperature[i] = heat[i];
        _stateDirty = false;
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

    private static float GetAmbientConductance(ThermalMaterial material) => material switch
    {
        ThermalMaterial.Air => Tweaks.World.ThermalKAmbientAir,
        ThermalMaterial.Dirt => Tweaks.World.ThermalKAmbientDirt,
        ThermalMaterial.Stone => Tweaks.World.EnableStoneAmbientExchange ? Tweaks.World.ThermalKAmbientStone : 0f,
        _ => Tweaks.World.ThermalKAmbientBase,
    };

    private static bool IsFixedTemperatureMaterial(ThermalMaterial material)
        => material == ThermalMaterial.Base;

    private static float GetFixedTemperature(ThermalMaterial material)
        => material == ThermalMaterial.Base ? Tweaks.World.ThermalFixedBaseTemperature : 0f;
}
