#pragma once

#include "render_surface.h"
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

    bool is_alive = false;

  public:
    void Advance(Level * level);
    void Draw(Surface * surface);

    bool IsInvalid() const { return !is_alive; }
    bool IsValid() const { return is_alive; }
    void Invalidate() { is_alive = false; }
};
