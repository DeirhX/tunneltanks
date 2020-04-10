#pragma once
#include "containers.h"
#include "harvester.h"

class HarvesterList
{
    using ProjectileContainer = MultiTypeContainer<Harvester>;
    /* Live items. Unmodified except for BEFORE Advance */
    ProjectileContainer items;
    /* Items here will be integrated into main vector on Advance */
    ProjectileContainer newly_created_items;

  public:
    HarvesterList() = default;

    template <typename THarverster>
    THarverster & Add(THarverster && projectile)
    {
        return this->newly_created_items.Add(projectile);
    }
    void Remove(Harvester & harvester) { harvester.Invalidate(); }
    void Shrink() { this->items.Shrink(); }

    void Advance(class Level * level, class TankList * tank_list);
    void Draw(class Surface * surface);
    Harvester * GetHarvesterAtPoint(Position position);
};
