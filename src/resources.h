#pragma once
#include <algorithm>
#include <cstdint>
#include <compare>

/*
 * ResourceAmount: A unit of resources able to perform arithmetic operations
 *                 Just so we can maintain strong type safety 
 */

template <typename ResourceType>
struct ResourceAmount
{
    int amount;
    ResourceAmount() = default;
    constexpr ResourceAmount(int amount) : amount(amount) {}
    constexpr ResourceAmount & operator=(ResourceType other)
    {
        this->amount = other.amount;
        return *this;
    }
    constexpr ResourceAmount & operator-=(ResourceType other)
    {
        this->amount -= other.amount;
        return *this;
    }
    constexpr ResourceAmount & operator+=(ResourceType other)
    {
        this->amount += other.amount;
        return *this;
    }
    constexpr ResourceType operator+(ResourceType right)
    { return ResourceType{this->amount + right.amount};
    }
    constexpr ResourceType operator-(ResourceType right)
    { return ResourceType{this->amount - right.amount};
    }
    auto operator<=>(const ResourceAmount & right) const = default;
};
/*
 * Amounts of concrete resources
 */
struct DirtAmount : public ResourceAmount<DirtAmount>
{
    DirtAmount() = default;
    constexpr DirtAmount(int amount) : ResourceAmount<DirtAmount>(amount) {}
};
struct MineralsAmount : public ResourceAmount<MineralsAmount>
{
    MineralsAmount() = default;
    constexpr MineralsAmount(int amount) : ResourceAmount<MineralsAmount>(amount) {}
};
struct EnergyAmount : public ResourceAmount<EnergyAmount>
{
    EnergyAmount() = default;
    constexpr EnergyAmount(int amount) : ResourceAmount<EnergyAmount>(amount) {}
};
struct HealthAmount : public ResourceAmount<HealthAmount>
{
    HealthAmount() = default;
    constexpr HealthAmount(int amount) : ResourceAmount<HealthAmount>(amount) {}
};

/* Beautiful literals. 10_health, 15_dirt, 35_minerals!  */
constexpr DirtAmount operator"" _dirt(std::uint64_t dirt_value) noexcept
{
    return DirtAmount{{static_cast<int>(dirt_value)}};
}
constexpr MineralsAmount operator"" _minerals(std::uint64_t mineral_value) noexcept
{
    return MineralsAmount{{static_cast<int>(mineral_value)}};
}
constexpr EnergyAmount operator"" _energy(std::uint64_t energy_value) noexcept
{
    return EnergyAmount{{static_cast<int>(energy_value)}};
}
constexpr HealthAmount operator"" _health(std::uint64_t health_value) noexcept
{
    return HealthAmount{{static_cast<int>(health_value)}};
}


template <typename FirstResourceType, typename SecondResourceType>
struct TwoResourceAmount
{
    FirstResourceType first = {};
    SecondResourceType second = {};

    TwoResourceAmount() = default;
    constexpr TwoResourceAmount(FirstResourceType first_) : first(first_) {}
    constexpr TwoResourceAmount(SecondResourceType second_) : second(second_) {}
    constexpr TwoResourceAmount(FirstResourceType first_, SecondResourceType second_) : first(first_), second(second_) {}

    TwoResourceAmount & operator+=(TwoResourceAmount other)
    {
        this->first += other.first;
        this->second += other.second;
        return *this;
    }
    TwoResourceAmount & operator-=(TwoResourceAmount other)
    {
        this->first -= other.first;
        this->second -= other.second;
        return *this;
    }
    friend constexpr TwoResourceAmount operator+(TwoResourceAmount left, TwoResourceAmount other)
    {
        return {{left.first + other.first}, {left.second + other.second}};
    }
    friend constexpr TwoResourceAmount operator-(TwoResourceAmount left, TwoResourceAmount other)
    {
        return {{left.first - other.first}, {left.second - other.second}};
    }
    bool IsNegative() { return this->first < 0 || this->second < 0; }
    void TrimNegative()
    {
        this->first = std::max(0, this->first.amount);
        this->second = std::max(0, this->second.amount);
    }
    auto operator <=> (const TwoResourceAmount & right) const = default;
};

/*
 * MaterialAmount:  Describe costs in a nice way
 */
struct MaterialAmount : public TwoResourceAmount<DirtAmount, MineralsAmount>
{
    using Parent = TwoResourceAmount<DirtAmount, MineralsAmount>;
public:
    MaterialAmount() = default;
    constexpr MaterialAmount(DirtAmount first_) : Parent(first_) {}
    constexpr MaterialAmount(MineralsAmount second_) : Parent(second_) {}
    constexpr MaterialAmount(DirtAmount first_, MineralsAmount second_) : Parent(first_, second_) { }

    DirtAmount & Dirt() { return this->first;}
    MineralsAmount & Minerals() { return this->second; }
    DirtAmount Dirt() const { return this->first; }
    MineralsAmount Minerals() const { return this->second; }
};

/*
 * Reactor: powers every destructable, has energy and health 
 */
struct ReactorCapacity
{
    EnergyAmount energy;
    HealthAmount health;
};

class Reactor
{
    EnergyAmount energy;
};

struct MaterialCapacity : public MaterialAmount
{
    
};
/*
 * Resources: Resource cache entities can possess
 */
class Materials
{
    MaterialAmount current;
    MaterialCapacity capacity;
  public:
    Materials(MaterialCapacity capacity_)
        : capacity(capacity_)
    { }
    Materials(DirtAmount dirt, MineralsAmount minerals, MaterialCapacity capacity_)
        : current(dirt, minerals), capacity(capacity_)
    { }
    /* true - paid the whole sum.  false - didn't have enough resources, state is unchanged */
    bool Pay(MaterialAmount payment);
    /* true - added the whole sum.  false - didn't have enough space, added as much as possible */
    bool Add(MaterialAmount gift);
    /* Absorb as much we have space for, subtracting it from argument */
    void Absorb(Materials & other);

    int GetDirt() const { return this->current.Dirt().amount; }
    int GetMinerals() const { return this->current.Minerals().amount; }
};


