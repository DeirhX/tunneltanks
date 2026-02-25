#pragma once
#include <memory>
#include <vector>
#include <array>
#include <span>
#include "types.h"
#include "entity.h"

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

struct Collider
{
};

class BitmapContainer
{
  private:
    Size size;
    Offset center;
    bool multiple_directions = false;
    std::vector<uint8_t> bitmap_data;

  private:
    std::span<uint8_t> RawDataForDirection(Direction direction);
    std::span<const uint8_t> RawDataForDirection(Direction direction) const;

  public:
    BitmapContainer(Size size, Offset center, std::span<uint8_t> collision_data);
    BitmapContainer(Size size, Offset center, std::span<std::span<uint8_t>, 9> multiple_directions);

    [[nodiscard]] Size Size() const { return this->size; }
    [[nodiscard]] Offset Center() const { return this->center; }

    BitmapView GetForDirection(Direction direction) const;
};

class BitmapCollision : public BitmapContainer
{
    using Base = BitmapContainer;

  public:
    using Base::Base;
};

struct BoundingBoxCollision
{
    // All needed info is in BoundingBox
};

class PointCollision
{
    // All needed info is in Position
};

} // namespace crust::components

namespace crust::aspects
{
using namespace components;
using BitmapCollidable = ecs::aspect<Position, BitmapCollision>;
using BoundingBoxCollidable = ecs::aspect<Position, BoundingBox, BoundingBoxCollision>;
using PointCollidable = ecs::aspect<Position, PointCollision>;
} // namespace crust::aspects
