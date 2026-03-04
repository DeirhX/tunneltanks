namespace Tunnerer.Core.Resources;

public readonly record struct Heat(int Value)
{
    public static implicit operator int(Heat h) => h.Value;
    public static Heat operator +(Heat a, Heat b) => new(a.Value + b.Value);
    public static Heat operator -(Heat a, Heat b) => new(a.Value - b.Value);
}

public readonly record struct Health(int Value)
{
    public static implicit operator int(Health h) => h.Value;
    public static Health operator +(Health a, Health b) => new(a.Value + b.Value);
    public static Health operator -(Health a, Health b) => new(a.Value - b.Value);
}

public struct ReactorState
{
    public Heat Heat;
    public Health Health;

    public ReactorState(Heat heat, Health health) { Heat = heat; Health = health; }
    public ReactorState(int heat, int health) { Heat = new(heat); Health = new(health); }

    public static ReactorState operator +(ReactorState a, ReactorState b) => new(a.Heat + b.Heat, a.Health + b.Health);
    public static ReactorState operator -(ReactorState a, ReactorState b) => new(a.Heat - b.Heat, a.Health - b.Health);
    public bool IsNegative => Heat < 0 || Health < 0;
    public void TrimNegative() { Heat = new(Math.Max(0, Heat)); Health = new(Math.Max(0, Health)); }
}

public struct MaterialAmount
{
    public int Dirt;
    public int Minerals;

    public MaterialAmount(int dirt, int minerals) { Dirt = dirt; Minerals = minerals; }

    public static MaterialAmount operator +(MaterialAmount a, MaterialAmount b) => new(a.Dirt + b.Dirt, a.Minerals + b.Minerals);
    public static MaterialAmount operator -(MaterialAmount a, MaterialAmount b) => new(a.Dirt - b.Dirt, a.Minerals - b.Minerals);
    public bool IsNegative => Dirt < 0 || Minerals < 0;
    public void TrimNegative() { Dirt = Math.Max(0, Dirt); Minerals = Math.Max(0, Minerals); }
}

public class Reactor
{
    public ReactorState Current;
    public ReactorState Capacity;

    public Reactor(ReactorState initial, ReactorState capacity)
    {
        Current = initial;
        Capacity = capacity;
    }

    public Heat Heat => Current.Heat;
    public Health Health => Current.Health;
    public Heat HeatCapacity => Capacity.Heat;
    public Health HealthCapacity => Capacity.Health;

    public bool CanPay(ReactorState cost) => !(Current - cost).IsNegative;

    public bool Pay(ReactorState cost)
    {
        if (!CanPay(cost)) return false;
        Current -= cost;
        return true;
    }

    public void Add(ReactorState gift)
    {
        Current += gift;
        var excess = Current - Capacity;
        excess.TrimNegative();
        Current -= excess;
    }

    public bool Exhaust(ReactorState reduction)
    {
        Current -= reduction;
        bool negative = Current.IsNegative;
        Current.TrimNegative();
        return !negative;
    }

    public void Absorb(Reactor other)
    {
        Current += other.Current;
        other.Current = default;
        var excess = Current - Capacity;
        excess.TrimNegative();
        Current -= excess;
        other.Current += excess;
    }
}

public class MaterialContainer
{
    public MaterialAmount Current;
    public MaterialAmount Capacity;

    public MaterialContainer(MaterialAmount initial, MaterialAmount capacity)
    {
        Current = initial;
        Capacity = capacity;
    }

    public int Dirt => Current.Dirt;
    public int Minerals => Current.Minerals;
    public int DirtCapacity => Capacity.Dirt;
    public int MineralsCapacity => Capacity.Minerals;

    public bool CanPay(MaterialAmount cost) => !(Current - cost).IsNegative;

    public bool Pay(MaterialAmount cost)
    {
        if (!CanPay(cost)) return false;
        Current -= cost;
        return true;
    }

    public void Add(MaterialAmount gift)
    {
        Current += gift;
        var excess = Current - Capacity;
        excess.TrimNegative();
        Current -= excess;
    }

    public void Absorb(MaterialContainer other)
    {
        Current += other.Current;
        other.Current = default;
        var excess = Current - Capacity;
        excess.TrimNegative();
        Current -= excess;
        other.Current += excess;
    }
}
