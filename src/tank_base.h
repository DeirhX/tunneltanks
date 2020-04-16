#pragma once
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
    static constexpr Size BaseSize = Size{tweak::base::BaseSize, tweak::base::BaseSize};

    Position position = {-1, -1};
    BoundingBox bounding_box = {BaseSize};
    TankColor color = {-1};
    LinkPoint * link_point{};

    Reactor reactor = tweak::base::Reactor;
    MaterialContainer resources = tweak::base::MaterialContainer;
  public:
    TankBase() = default;
    explicit TankBase(Position position, TankColor color);

    void RegisterLinkPoint(World * world);

    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] TankColor GetColor() const { return this->color; }
    [[nodiscard]] const MaterialContainer & GetResources() const { return this->resources; }
    [[nodiscard]] bool IsInside(Position position) const;
    
    void AbsorbResources(MaterialContainer & other);
    void AbsorbResources(MaterialContainer & other, MaterialAmount rate);

    void Draw(Surface * surface) const;
    void Advance();
};
