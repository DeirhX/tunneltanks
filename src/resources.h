#pragma once
#include <cstdint>

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
};
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

/*
 * Cost:  Describe costs in a nice way
 */
struct Cost
{
    DirtAmount dirt = {};
    MineralsAmount minerals = {};

    Cost() = default;
    constexpr Cost(DirtAmount dirt) : dirt(dirt) {}
    constexpr Cost(MineralsAmount minerals) : minerals(minerals) {}
    constexpr Cost(DirtAmount dirt, MineralsAmount minerals) : dirt(dirt), minerals(minerals) {}
    friend constexpr Cost operator+(Cost left, Cost other)
    {
        return {{left.dirt + other.dirt}, {left.minerals + other.minerals}};
    }
};
constexpr DirtAmount operator"" _dirt(std::uint64_t dirt_value) noexcept
{
    return DirtAmount{{static_cast<int>(dirt_value)}};
}
constexpr MineralsAmount operator"" _minerals(std::uint64_t mineral_value) noexcept
{
    return MineralsAmount{{static_cast<int>(mineral_value)}};
}

struct ResourceCapacity
{
    Cost value;
};
/*
 * Resources: Resource cache entities can posses
 */

class Resources
{
    DirtAmount dirt = {};
    MineralsAmount minerals = {};

    DirtAmount dirt_max;
    MineralsAmount minerals_max;
  public:
    Resources(ResourceCapacity capacity)
        : dirt_max(capacity.value.dirt), minerals_max(capacity.value.minerals)
    { }
    Resources(DirtAmount dirt, MineralsAmount minerals, ResourceCapacity capacity)
        : dirt(dirt), minerals(minerals), dirt_max(capacity.value.dirt), minerals_max(capacity.value.minerals)
    { }
    /* true - paid the whole sum.  false - didn't have enough resources, state is unchanged */
    bool Pay(Cost payment);
    /* true - added the whole sum.  false - didn't have enough space, added as much as possible */
    bool Add(Cost gift);
    /* Absorb as much we have space for, subtracting it from argument */
    void Absorb(Resources & other);
    int GetDirt() const { return this->dirt.amount; }
    int GetMinerals() const { return this->minerals.amount; }
};
