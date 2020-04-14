#pragma once
#include "link.h"
#include "resources.h"
#include "types.h"

class World;

/*
 * Tank Base, part of the level
 */
class TankBase
{
    Position position = {-1, -1};
    LinkPoint * link_point{};

    Resources resources = {0_dirt, 0_minerals, ResourceCapacity{Cost{10000_dirt, 10000_minerals}}};
  public:
    TankBase() = default;
    explicit TankBase(Position position);

    void RegisterLinkPoint(World * world);

    Position GetPosition() const { return this->position; }
    void AbsorbResources(Resources & resources);
};
