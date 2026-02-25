#include "pch.h"
#include "collision_component.h"
#include "types.h"


namespace crust::components
{

std::span<uint8_t> BitmapContainer::RawDataForDirection(Direction direction) 
{
    assert(!bitmap_data.empty());
    return {bitmap_data.begin() + direction * size.Area(), static_cast<size_t>(size.Area())};
}

std::span<const uint8_t> BitmapContainer::RawDataForDirection(Direction direction) const
{
    assert(!bitmap_data.empty());
    return {bitmap_data.begin() + direction * size.Area(), static_cast<size_t>(size.Area())};
}

BitmapContainer::BitmapContainer(crust::Size size, Offset center, std::span<uint8_t> bitmap_data)
    : size(size), center(center), bitmap_data(bitmap_data.begin(), bitmap_data.end())
{
    if (bitmap_data.size() == size.Area())
        multiple_directions = false;
    else if (bitmap_data.size() == 9 * size.Area())
        multiple_directions = true;
    else
        throw std::logic_error("Invalid size of bitmap_data");
}

BitmapContainer::BitmapContainer(crust::Size size, Offset center, std::span<std::span<uint8_t>, 9> multiple_directions)
    : size(size), center(center), multiple_directions(true)
{
    bitmap_data.resize(size.Area() * 9);
    int directions = 9;
    for (int dir = 0; dir < directions; ++dir)
    {
        auto slot = RawDataForDirection(Direction(dir));
        assert(multiple_directions[dir].size() == size.Area());
        std::ranges::copy(multiple_directions[dir], slot.begin());
    }
}

BitmapView BitmapContainer::GetForDirection(Direction direction) const
{
    assert(!bitmap_data.empty());
    auto raw_data = RawDataForDirection(direction);
    return {std::span{raw_data.begin(), raw_data.size()}, size};
}

} // namespace components
