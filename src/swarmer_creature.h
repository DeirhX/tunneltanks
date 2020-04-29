#pragma once
#include "controllable.h"

class Swarmer : Controllable
{
    using Base = Controllable;

    BoundingBox bounding_box = {Size{5, 5}};

  public:
    Swarmer(Position position, class Level * level);

    CollisionType TryCollide(Direction at_rotation, Position at_position) override;
    void ApplyControllerOutput(ControllerOutput controls) override;
    void Die() override;
};
