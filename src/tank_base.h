#pragma once
#include "link.h"
#include "resources.h"
#include "tweak.h"
#include "types.h"

class World;

/*
 * Tank Base, part of the level
 */
class TankBase
{
    Position position = {-1, -1};
    BoundingBox bounding_box = {Size{tweak::world::BaseSize, tweak::world::BaseSize}};
    TankColor color = {-1};
    LinkPoint * link_point{};

    Resources resources = {0_dirt, 0_minerals, ResourceCapacity{Cost{10000_dirt, 10000_minerals}}};
  public:
    TankBase() = default;
    explicit TankBase(Position position, TankColor color);

    void RegisterLinkPoint(World * world);

    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] TankColor GetColor() const { return this->color; }
    [[nodiscard]] const Resources & GetResources() const { return this->resources; }
    [[nodiscard]] bool IsInside(Position position) const;
    
    void AbsorbResources(Resources & other);
    void AbsorbResources(Resources & other, Cost rate);
};
