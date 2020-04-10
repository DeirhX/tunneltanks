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
    bool is_alive = true;

    RepetitiveTimer harvest_timer{tweak::rules::HarvestTimer};
  public:
    Harvester(Position position, HarvesterType type);

    void Advance(Level * level);
    void Draw(Surface * surface) const;

    bool IsInvalid() const { return !is_alive; }
    bool IsValid() const { return is_alive; }
    void Invalidate() { is_alive = false; }
    void AlterHealth(int shot_damage);
private:
    void Die(Level * level);
};
