#pragma once
#include <memory>
#include <vector>
#include <array>
#include <span>
#include "types.h"

namespace crust::components
{

class BitmapView
{
    Size size;
    std::span<const uint8_t> data_view;

  public:
    BitmapView(std::span<const uint8_t> source, Size size) : size(size), data_view(source)
    {
        assert(size.Area() == data_view.size());
    }
    uint8_t GetAt(Offset offset) const
    {
        assert(size.FitsInside(offset));
        return data_view[offset.y * size.x + offset.x];
    }
};

class BitmapCollision
{
  private:
    Size size;
    Offset center;
    bool multiple_directions = false;
    std::vector<uint8_t> collision_data;

  private:
    std::span<uint8_t> RawDataForDirection(Direction direction);
    std::span<const uint8_t> RawDataForDirection(Direction direction) const;
  public:
    BitmapCollision(Size size, Offset center, std::span<uint8_t> collision_data);
    BitmapCollision(Size size, Offset center, std::span<std::span<uint8_t>, 9> multiple_directions);

    [[nodiscard]] Size Size() const { return this->size; }
    [[nodiscard]] Offset Center() const { return this->center; }

    BitmapView GetForDirection(Direction direction) const;

    //uint8_t GetRelative(Offset offset) const
    //{
    //    Offset data_offset = offset + center;
    //    if (!size.FitsInside(data_offset))
    //    {
    //        return {0};
    //    }
    //    return collision_data[data_offset.x + data_offset.y * size.x];
    //}
};



} // namespace crust::components
