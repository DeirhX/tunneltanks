#include "pch.h"
#include "collision_component.h"
#include "types.h"


namespace crust::components
{

std::span<uint8_t> BitmapCollision::RawDataForDirection(Direction direction) 
{
    assert(!collision_data.empty());
    return {collision_data.begin() + direction * size.Area(), static_cast<size_t>(size.Area())};
}

std::span<const uint8_t> BitmapCollision::RawDataForDirection(Direction direction) const
{
    assert(!collision_data.empty());
    return {collision_data.begin() + direction * size.Area(), static_cast<size_t>(size.Area())};
}

BitmapCollision::BitmapCollision(crust::Size size, Offset center, std::span<uint8_t> collision_data)
    : size(size), center(center), collision_data(collision_data.begin(), collision_data.end())
{
    if (collision_data.size() == size.Area())
        multiple_directions = false;
    else if (collision_data.size() == 9 * size.Area())
        multiple_directions = true;
    else
        throw std::logic_error("Invalid size of collision_data");
}

BitmapCollision::BitmapCollision(crust::Size size, Offset center, std::span<std::span<uint8_t>, 9> multiple_directions)
    : size(size), center(center), multiple_directions(true)
{
    collision_data.resize(size.Area() * 9);
    int directions = 9;
    for (int dir = 0; dir < directions; ++dir)
    {
        auto slot = RawDataForDirection(Direction(dir));
        assert(multiple_directions[dir].size() == size.Area());
        std::ranges::copy(multiple_directions[dir], slot.begin());
    }
}

BitmapView BitmapCollision::GetForDirection(Direction direction) const
{
    assert(!collision_data.empty());
    auto raw_data = RawDataForDirection(direction);
    return {std::span{raw_data.begin(), raw_data.size()}, size};
}

} // namespace components
