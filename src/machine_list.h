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
    template <typename TMachine>
    TMachine & Add(TMachine && projectile)
    {
        return this->newly_created_items.Add(projectile);
    }
    template <typename TMachine, typename... TConstructArgs>
    TMachine & Emplace(TConstructArgs... args)
    {
        return this->newly_created_items.ConstructElement<TMachine>(std::forward<TConstructArgs>(args)...);
    }
    void Remove(Machine & harvester) { harvester.Invalidate(); }
    void RemoveAll() { this->items.RemoveAll(); }
    void Shrink() { this->items.Shrink(); }

    void Advance(class Level * level, class TankList * tank_list);
    void Draw(class Surface * surface);

    Machine * GetMachineAtPoint(Position position);
};
