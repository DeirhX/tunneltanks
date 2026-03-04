namespace Tunnerer.Core.Config;

/// <summary>
/// Lightweight deterministic RNG for simulation hot paths.
/// </summary>
public struct FastRandom
{
    private uint _state;

    public FastRandom(int seed) : this((uint)seed) { }

    public FastRandom(uint seed)
    {
        _state = seed == 0 ? 0x9E3779B9u : seed;
    }

    public uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 1) return 0;
        return (int)(NextUInt() % (uint)maxExclusive);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return minInclusive + NextInt(maxExclusive - minInclusive);
    }

    public float NextSingle()
    {
        return (NextUInt() & 0x00FFFFFFu) / 16777216f;
    }

    public bool Chance1000(int threshold)
    {
        if (threshold <= 0) return false;
        if (threshold >= 1000) return true;
        return NextInt(1000) < threshold;
    }

    public static uint Hash32(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }
}
