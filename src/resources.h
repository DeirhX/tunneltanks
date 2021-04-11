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
    using ResourceAmount<DirtAmount>::ResourceAmount;
};
struct MineralsAmount : public ResourceAmount<MineralsAmount>
{
    using ResourceAmount<MineralsAmount>::ResourceAmount;
};
struct EnergyAmount : public ResourceAmount<EnergyAmount>
{
    using ResourceAmount<EnergyAmount>::ResourceAmount;
};
struct HealthAmount : public ResourceAmount<HealthAmount>
{
    using ResourceAmount<HealthAmount>::ResourceAmount;
};

/* Beautiful literals. 10_health, 15_dirt, 35_minerals!  */
constexpr DirtAmount operator"" _dirt(std::uint64_t dirt_value) noexcept
{
    return DirtAmount{static_cast<int>(dirt_value)};
}
constexpr MineralsAmount operator"" _minerals(std::uint64_t mineral_value) noexcept
{
    return MineralsAmount{static_cast<int>(mineral_value)};
}
constexpr EnergyAmount operator"" _energy(std::uint64_t energy_value) noexcept
{
    return EnergyAmount{static_cast<int>(energy_value)};
}
constexpr HealthAmount operator"" _health(std::uint64_t health_value) noexcept
{
    return HealthAmount{static_cast<int>(health_value)};
}


/*
 * Template for binding two resources together. It can be chained further.
 */
template <typename FirstResourceType, typename SecondResourceType>
struct TwoResourceAmount
{
    FirstResourceType first = {};
    SecondResourceType second = {};

    constexpr TwoResourceAmount() = default;
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
    bool IsNegative() { return this->first.amount < 0 || this->second.amount < 0; }
    void TrimNegative()
    {
        this->first = std::max(0, this->first.amount);
        this->second = std::max(0, this->second.amount);
    }
    auto operator <=> (const TwoResourceAmount & right) const = default;
};

/*
 * MaterialAmount:  Describe material costs in a nice way
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

struct MaterialCapacity : public MaterialAmount
{
};

/*
 * Reactor: powers every destructable, has energy and health 
 */
struct ReactorState : public TwoResourceAmount<EnergyAmount, HealthAmount>
{
    using Parent = TwoResourceAmount<EnergyAmount, HealthAmount>;

  public:
    constexpr ReactorState() = default;
    constexpr ReactorState(EnergyAmount first_) : Parent(first_) {}
    constexpr ReactorState(HealthAmount second_) : Parent(second_) {}
    constexpr ReactorState(EnergyAmount first_, HealthAmount second_) : Parent(first_, second_) {}

    EnergyAmount & Energy() { return this->first; }
    HealthAmount & Health() { return this->second; }
    EnergyAmount Energy() const { return this->first; }
    HealthAmount Health() const { return this->second; }
};

struct ReactorCapacity : public ReactorState
{
};

/*
 * Resources: Resource cache entities can possess
 */
template <typename AmountType, typename CapacityType>
class ResourceContainer
{
  protected:
    AmountType current;
    CapacityType capacity;

  public:
    constexpr ResourceContainer(CapacityType capacity_) : capacity(capacity_) {}
    constexpr ResourceContainer(AmountType amount_, CapacityType capacity_)
        : current(amount_), capacity(capacity_)
    {
    }
    bool CanPay(AmountType payment) const;
    /* true - paid the whole sum.  false - didn't have enough resources, state is unchanged */
    bool Pay(AmountType payment);
    /* true - added the whole sum.  false - didn't have enough space, added as much as possible */
    bool Add(AmountType gift);
    /* true - removed the whole sum.  false - didn't have enough enough, reached zero in some members */
    bool Exhaust(AmountType reduction);
    /* Absorb as much we have space for, subtracting it from argument */
    void Absorb(ResourceContainer & other);

    void Fill() { this->current = this->capacity; }
    void Clear() { this->current = {}; }
    void TrimNegative() { this->current.TrimNegative(); }
};

template <typename AmountType, typename CapacityType>
bool ResourceContainer<AmountType, CapacityType>::CanPay(AmountType payment) const
{
    return !((this->current - payment).IsNegative());
}


template <typename AmountType, typename CapacityType>
bool ResourceContainer<AmountType, CapacityType>::Pay(AmountType payment)
{
    if (!CanPay(payment))
        return false;
    this->current -= payment;
    return true;
}

/* true - added the whole sum.  false - didn't have enough space, added as much as possible */
template <typename AmountType, typename CapacityType>
bool ResourceContainer<AmountType, CapacityType>::Add(AmountType gift)
{
    bool exceeded = false;
    if (this->current + gift > this->capacity)
        exceeded = true;

    /* Do the add */
    this->current += gift;

    /* Trim if it was too much */
    auto excess = this->current - this->capacity;
    excess.TrimNegative();
    this->current -= excess;

    return exceeded;
}

template <typename AmountType, typename CapacityType>
bool ResourceContainer<AmountType, CapacityType>::Exhaust(AmountType reduction)
{
    this->current -= reduction;
    bool negative = this->current.IsNegative();
    this->current.TrimNegative();
    return !negative;
}

template <typename AmountType, typename CapacityType>
void ResourceContainer<AmountType, CapacityType>::Absorb(ResourceContainer & other)
{
    this->current += other.current;
    other.current = {};

    auto excess = this->current - this->capacity;
    excess.TrimNegative();
    this->current -= excess;
    other.current += excess;
}


/*
 * Container for gathered materials that can be transferred with other container
 */
class MaterialContainer : public ResourceContainer<MaterialAmount, MaterialCapacity>
{
    using Parent = ResourceContainer<MaterialAmount, MaterialCapacity>;
  public:
    constexpr MaterialContainer(MaterialCapacity capacity_) : Parent(capacity_) {}
    constexpr MaterialContainer(DirtAmount dirt, MineralsAmount minerals, MaterialCapacity capacity_)
        : Parent({dirt, minerals}, capacity_)
    { }

    int GetDirt() const { return this->current.Dirt().amount; }
    int GetMinerals() const { return this->current.Minerals().amount; }
    int GetDirtCapacity() const { return this->capacity.Dirt().amount; }
    int GetMineralsCapacity() const { return this->capacity.Minerals().amount; }
};

/*
 * Everything that has energy has also health. Everything with health may have also energy.
 */
class Reactor : public ResourceContainer<ReactorState, ReactorCapacity>
{
    using Parent = ResourceContainer<ReactorState, ReactorCapacity>;

  public:
    constexpr Reactor(ReactorCapacity capacity_) : Parent(capacity_) {}
    constexpr Reactor(EnergyAmount energy, HealthAmount health, ReactorCapacity capacity_)
        : Parent({energy, health}, capacity_)
    { }

    int GetHealth() const { return this->current.Health().amount; }
    int GetHealthCapacity() const { return this->capacity.Health().amount; }
    int GetEnergy() const { return this->current.Energy().amount; }
    int GetEnergyCapacity() const { return this->capacity.Energy().amount; }
};


