#pragma once

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

  public:
    void Advance(Level * level);
    //void Draw
    
};
