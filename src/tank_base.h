#pragma once
#include "types.h"

/*
 * Tank Base, part of the level
 */
class TankBase
{
    Position position = {-1, -1};

  public:
    TankBase() = default;
    TankBase(Position position) : position(position) {}

    Position GetPosition() const { return this->position; }
};
