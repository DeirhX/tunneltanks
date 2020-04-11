#pragma once
#include "containers.h"
#include "machine.h"

class MachineryList
{
    using Container = MultiTypeContainer<Harvester, Charger>;
    /* Live items. Unmodified except for BEFORE Advance */
    Container items;
    /* Items here will be integrated into main vector on Advance */
    Container newly_created_items;

  public:
    MachineryList() = default;

    /* Add/remove  */
    template <typename THarverster>
    THarverster & Add(THarverster && projectile)
    {
        return this->newly_created_items.Add(projectile);
    }
    void Remove(Machine & harvester) { harvester.Invalidate(); }
    void Shrink() { this->items.Shrink(); }

    void Advance(class Level * level, class TankList * tank_list);
    void Draw(class Surface * surface);

    Machine * GetMachineAtPoint(Position position);
};
