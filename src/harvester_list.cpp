#include "harvester_list.h"

void HarvesterList::Advance(Level * level, TankList * tank_list)
{
    /* Append everything that was created last tick */
    this->items.MergeFrom(this->newly_created_items);
    this->newly_created_items.RemoveAll();
    Shrink();

    /* Advance everything */
    this->items.ForEach([level](Harvester & item) { item.Advance(level); });
}

void HarvesterList::Draw(Surface * surface)
{ 
    this->items.ForEach([surface](Harvester & item) { item.Draw(surface); });
}

Harvester * HarvesterList::GetHarvesterAtPoint(Position position)
{
    return nullptr;
}
