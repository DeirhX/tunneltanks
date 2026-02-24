namespace TunnelTanks.Core.Resources;

public struct ReactorState
{
    public int Energy;
    public int Health;

    public ReactorState(int energy, int health) { Energy = energy; Health = health; }

    public static ReactorState operator +(ReactorState a, ReactorState b) => new(a.Energy + b.Energy, a.Health + b.Health);
    public static ReactorState operator -(ReactorState a, ReactorState b) => new(a.Energy - b.Energy, a.Health - b.Health);
    public bool IsNegative => Energy < 0 || Health < 0;
    public void TrimNegative() { Energy = Math.Max(0, Energy); Health = Math.Max(0, Health); }
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

    public Reactor(int energy, int health, int energyCap, int healthCap)
    {
        Current = new(energy, health);
        Capacity = new(energyCap, healthCap);
    }

    public int Energy => Current.Energy;
    public int Health => Current.Health;
    public int EnergyCapacity => Capacity.Energy;
    public int HealthCapacity => Capacity.Health;

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

    public MaterialContainer(int dirt, int minerals, int dirtCap, int mineralsCap)
    {
        Current = new(dirt, minerals);
        Capacity = new(dirtCap, mineralsCap);
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
