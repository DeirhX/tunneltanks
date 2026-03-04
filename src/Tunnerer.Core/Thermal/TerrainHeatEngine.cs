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
    private float[]? _airDelta;

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

    public void StepConservative(float[] terrainTemperature, float[] airTemperature, TerrainPixel[] pixels, int width, int height)
    {
        int len = width * height;
        if (terrainTemperature.Length < len || airTemperature.Length < len)
            throw new ArgumentException("temperature lengths must cover simulation grid");

        if (_delta == null || _delta.Length < len)
            _delta = new float[len];
        if (_airDelta == null || _airDelta.Length < len)
            _airDelta = new float[len];
        Array.Clear(_delta, 0, len);
        Array.Clear(_airDelta, 0, len);

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int idx = row + x;
                ThermalMaterial m1 = Pixel.GetThermalMaterial(pixels[idx]);
                float t1 = terrainTemperature[idx];
                float a1 = airTemperature[idx];

                // In-cell coupling between matter and local air volume.
                ExchangeTerrainAir(idx, m1, t1, a1, _delta!, _airDelta!);

                if (x + 1 < width)
                {
                    int idxR = idx + 1;
                    ThermalMaterial mR = Pixel.GetThermalMaterial(pixels[idxR]);
                    float tR = terrainTemperature[idxR];
                    float aR = airTemperature[idxR];
                    ExchangeTerrainPair(idx, idxR, m1, t1, mR, tR, _delta!);
                    ExchangeAirPair(idx, idxR, a1, aR, _airDelta!);
                }
                if (y + 1 < height)
                {
                    int idxD = idx + width;
                    ThermalMaterial mD = Pixel.GetThermalMaterial(pixels[idxD]);
                    float tD = terrainTemperature[idxD];
                    float aD = airTemperature[idxD];
                    ExchangeTerrainPair(idx, idxD, m1, t1, mD, tD, _delta!);
                    ExchangeAirPair(idx, idxD, a1, aD, _airDelta!);
                }
            }
        }

        for (int i = 0; i < len; i++)
        {
            terrainTemperature[i] += _delta[i];
            airTemperature[i] += _airDelta[i];
            if (Pixel.GetThermalMaterial(pixels[i]) == ThermalMaterial.Air)
                terrainTemperature[i] = airTemperature[i];
        }
    }

    public void ComputeRegionDeltaConservative(
        float[] terrainTemperature,
        float[] airTemperature,
        TerrainPixel[] pixels,
        int width,
        int height,
        int minX,
        int minY,
        int maxXInclusive,
        int maxYInclusive,
        float[] terrainDelta,
        float[] airDelta)
    {
        int rw = maxXInclusive - minX + 1;
        int rh = maxYInclusive - minY + 1;
        int required = rw * rh;
        if (terrainDelta.Length < required || airDelta.Length < required)
            throw new ArgumentException("region delta lengths must cover region area");
        Array.Clear(terrainDelta, 0, required);
        Array.Clear(airDelta, 0, required);

        for (int y = minY; y <= maxYInclusive; y++)
        {
            int row = y * width;
            for (int x = minX; x <= maxXInclusive; x++)
            {
                int idx = row + x;
                int local = (y - minY) * rw + (x - minX);
                ThermalMaterial m1 = Pixel.GetThermalMaterial(pixels[idx]);
                float t1 = terrainTemperature[idx];
                float a1 = airTemperature[idx];

                ExchangeTerrainAir(local, m1, t1, a1, terrainDelta, airDelta);

                if (x + 1 <= maxXInclusive)
                {
                    int idxR = idx + 1;
                    int localR = local + 1;
                    ThermalMaterial mR = Pixel.GetThermalMaterial(pixels[idxR]);
                    float tR = terrainTemperature[idxR];
                    float aR = airTemperature[idxR];
                    ExchangeTerrainPair(local, localR, m1, t1, mR, tR, terrainDelta);
                    ExchangeAirPair(local, localR, a1, aR, airDelta);
                }
                if (y + 1 <= maxYInclusive)
                {
                    int idxD = idx + width;
                    int localD = local + rw;
                    ThermalMaterial mD = Pixel.GetThermalMaterial(pixels[idxD]);
                    float tD = terrainTemperature[idxD];
                    float aD = airTemperature[idxD];
                    ExchangeTerrainPair(local, localD, m1, t1, mD, tD, terrainDelta);
                    ExchangeAirPair(local, localD, a1, aD, airDelta);
                }
            }
        }
    }

    public double SumTotalEnergy(float[] terrainTemperature, float[] airTemperature, TerrainPixel[] pixels)
    {
        if (pixels.Length < terrainTemperature.Length || airTemperature.Length < terrainTemperature.Length)
            throw new ArgumentException("state lengths must cover internal state");

        double sum = 0.0;
        for (int i = 0; i < terrainTemperature.Length; i++)
        {
            ThermalMaterial m = Pixel.GetThermalMaterial(pixels[i]);
            float cTerrain = GetHeatCapacity(m);
            sum += terrainTemperature[i] * cTerrain;
            sum += airTemperature[i] * Tweaks.World.ThermalCapacityAir;
        }

        return sum;
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

    public void ComputeRegionDelta(
        float[] temperature,
        TerrainPixel[] pixels,
        int width,
        int height,
        bool includeAmbientExchange,
        int minX,
        int minY,
        int maxXInclusive,
        int maxYInclusive,
        float[] regionDelta)
    {
        int rw = maxXInclusive - minX + 1;
        int rh = maxYInclusive - minY + 1;
        int required = rw * rh;
        if (regionDelta.Length < required)
            throw new ArgumentException("regionDelta length must cover region area", nameof(regionDelta));
        Array.Clear(regionDelta, 0, required);

        for (int y = minY; y <= maxYInclusive; y++)
        {
            int row = y * width;
            for (int x = minX; x <= maxXInclusive; x++)
            {
                int idx = row + x;
                int local = (y - minY) * rw + (x - minX);
                ThermalMaterial m1 = Pixel.GetThermalMaterial(pixels[idx]);
                float t1 = temperature[idx];

                if (x + 1 <= maxXInclusive)
                    ExchangePairRegion(idx, idx + 1, local, local + 1, m1, t1, pixels, temperature, regionDelta);
                if (y + 1 <= maxYInclusive)
                    ExchangePairRegion(idx, idx + width, local, local + rw, m1, t1, pixels, temperature, regionDelta);
                if (includeAmbientExchange)
                    ExchangeAmbientRegion(local, m1, t1, regionDelta);
            }
        }
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

    private static void ExchangeTerrainPair(
        int idxA,
        int idxB,
        ThermalMaterial mA,
        float tA,
        ThermalMaterial mB,
        float tB,
        float[] terrainDelta)
    {
        if (mA == ThermalMaterial.Air || mB == ThermalMaterial.Air)
            return;

        float delta = tA - tB;
        if (MathF.Abs(delta) < 0.0001f)
            return;

        float cA = GetHeatCapacity(mA);
        float cB = GetHeatCapacity(mB);
        float k = GetConductance(mA, mB);
        float dQ = StablePairHeatFlow(delta, cA, cB, k);
        if (MathF.Abs(dQ) < 0.0001f)
            return;

        terrainDelta[idxA] -= dQ / cA;
        terrainDelta[idxB] += dQ / cB;
    }

    private static void ExchangeAirPair(int idxA, int idxB, float aA, float aB, float[] airDelta)
    {
        float delta = aA - aB;
        if (MathF.Abs(delta) < 0.0001f)
            return;

        float c = Tweaks.World.ThermalCapacityAir;
        float dQ = StablePairHeatFlow(delta, c, c, Tweaks.World.ThermalKAirAir * 0.5f + Tweaks.World.ThermalAirNeighborCoupling);
        if (MathF.Abs(dQ) < 0.0001f)
            return;

        airDelta[idxA] -= dQ / c;
        airDelta[idxB] += dQ / c;
    }

    private void ExchangeTerrainAir(int idx, ThermalMaterial material, float terrainT, float airT, float[] terrainDelta, float[] airDelta)
    {
        float delta = terrainT - airT;
        if (MathF.Abs(delta) < 0.0001f)
            return;

        float cTerrain = material == ThermalMaterial.Air
            ? Tweaks.World.ThermalCapacityAir
            : GetHeatCapacity(material);
        float cAir = Tweaks.World.ThermalCapacityAir;
        float k = material == ThermalMaterial.Air
            ? _settings.ThermalAirCellCoupling
            : GetConductance(material, ThermalMaterial.Air) + 0.5f * _settings.ThermalAirCellCoupling;
        float dQ = StablePairHeatFlow(delta, cTerrain, cAir, k);
        if (MathF.Abs(dQ) < 0.0001f)
            return;

        terrainDelta[idx] -= dQ / cTerrain;
        airDelta[idx] += dQ / cAir;
    }

    private static float StablePairHeatFlow(float deltaT, float cA, float cB, float k)
    {
        float dQ = k * deltaT * Tweaks.World.ThermalDt;
        float maxStableDQ = MathF.Min(cA, cB) * MathF.Abs(deltaT) * 0.125f;
        if (dQ > maxStableDQ) dQ = maxStableDQ;
        else if (dQ < -maxStableDQ) dQ = -maxStableDQ;
        return dQ;
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

    private static void ExchangePairRegion(
        int idxA,
        int idxB,
        int localA,
        int localB,
        ThermalMaterial mA,
        float tA,
        TerrainPixel[] pixels,
        float[] temperature,
        float[] regionDelta)
    {
        ThermalMaterial mB = Pixel.GetThermalMaterial(pixels[idxB]);
        float tB = temperature[idxB];
        float delta = tA - tB;
        if (MathF.Abs(delta) < 0.0001f)
            return;

        float cA = GetHeatCapacity(mA);
        float cB = GetHeatCapacity(mB);
        float k = GetConductance(mA, mB);
        float dQ = k * delta * Tweaks.World.ThermalDt;
        float maxStableDQ = MathF.Min(cA, cB) * MathF.Abs(delta) * 0.125f;
        if (dQ > maxStableDQ) dQ = maxStableDQ;
        else if (dQ < -maxStableDQ) dQ = -maxStableDQ;
        if (MathF.Abs(dQ) < 0.0001f)
            return;

        regionDelta[localA] -= dQ / cA;
        regionDelta[localB] += dQ / cB;
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

    private void ExchangeAmbientRegion(int localIdx, ThermalMaterial material, float temperature, float[] regionDelta)
    {
        float ambient = Tweaks.World.ThermalAmbientTemperature;
        if (temperature <= ambient)
            return;

        float k = GetAmbientConductance(material);
        float dQ = k * (ambient - temperature) * Tweaks.World.ThermalDt;
        if (MathF.Abs(dQ) < 0.0001f)
            return;

        float capacity = GetHeatCapacity(material);
        regionDelta[localIdx] += dQ / capacity;
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
