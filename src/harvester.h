#pragma once

#include "render_surface.h"
#include "tweak.h"
#include "types.h"

class Level;

enum class HarvesterType
{
    Dirt,
    Mineral,
};

class Harvester
{
    Position position;
    HarvesterType type;
    int health = tweak::rules::HarvesterHP;
    bool is_alive = false;

  public:
    Harvester(Position position, HarvesterType type);

    void Advance(Level * level);
    void Draw(Surface * surface);

    bool IsInvalid() const { return !is_alive; }
    bool IsValid() const { return is_alive; }
    void Invalidate() { is_alive = false; }
  private:
    void Die(Level * level);
};
