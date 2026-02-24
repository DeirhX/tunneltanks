namespace TunnelTanks.Core.Config;

public sealed class RandomGenerator
{
    private readonly Random _rng;

    public RandomGenerator() => _rng = new Random();
    public RandomGenerator(int seed) => _rng = new Random(seed);

    public int Int(int min, int max) => _rng.Next(min, max + 1);
    public int Int(int max) => _rng.Next(max);
    public bool Bool(int chanceOutOf1000) => _rng.Next(1000) < chanceOutOf1000;
    public float Float() => (float)_rng.NextDouble();

    public RandomGenerator Fork() => new(_rng.Next());
}
