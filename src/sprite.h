﻿#pragma once
#include "containers.h"
#include "duration.h"

class Level;
class Surface;

/*
 * Sprite: a drawable, immaterial entity that can be drawn even outside the normal pixel grid (not used yet)
 */
class Sprite : public Invalidable
{
  private:
    Position position;

  public:
    Sprite(Position position_) : position(position_) {}
    virtual void Draw(Surface * surface) const = 0;
    virtual void Advance(Level * level) {};

    void SetPosition(Position new_position) { this->position = new_position; }
    [[nodiscard]] Position GetPosition() const { return this->position; }
};

/*
 * FailedInteractionCross
 */
class FailedInteraction : public Sprite
{
    ManualTimer destroy_timer{std::chrono::milliseconds{1000}};

  public:
    void Advance(Level * level) override;
    void Draw(Surface * surface) const override;
};