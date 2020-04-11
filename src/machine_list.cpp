#include "machine_list.h"

void MachineryList::Advance(Level * level, TankList * tank_list)
{
    /* Append everything that was created last tick */
    this->items.MergeFrom(this->newly_created_items);
    this->newly_created_items.RemoveAll();
    Shrink();

    /* Advance everything */
    this->items.ForEach([level](auto & item) { item.Advance(level); });
}

void MachineryList::Draw(Surface * surface)
{ 
    this->items.ForEach([surface](auto & item) { item.Draw(surface); });
}

Machine * MachineryList::GetMachineAtPoint(Position position) 
{
    Machine * result = nullptr;
    this->items.ForEach([position, &result](auto & harvester) -> Machine * {
        if (harvester.IsColliding(position))
            result = &harvester;
    });
    return result;
}
