﻿#pragma once
#include "link.h"
#include "resources.h"
#include "tweak.h"
#include "types.h"

class Surface;
class World;

/*
 * Tank Base, part of the level
 */
class TankBase
{
    static constexpr Size BaseSize = Size{tweak::world::BaseSize, tweak::world::BaseSize};

    Position position = {-1, -1};
    BoundingBox bounding_box = {BaseSize};
    TankColor color = {-1};
    LinkPoint * link_point{};

    Materials resources = {0_dirt, 0_minerals, MaterialCapacity{MaterialAmount{1000_dirt, 1000_minerals}}};
  public:
    TankBase() = default;
    explicit TankBase(Position position, TankColor color);

    void RegisterLinkPoint(World * world);

    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] TankColor GetColor() const { return this->color; }
    [[nodiscard]] const Materials & GetResources() const { return this->resources; }
    [[nodiscard]] bool IsInside(Position position) const;
    
    void AbsorbResources(Materials & other);
    void AbsorbResources(Materials & other, MaterialAmount rate);

    void Draw(Surface * surface) const;
    void Advance() {}
};
