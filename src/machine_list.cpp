#include "machine_list.h"

void MachineryList::Advance(Level * level, TankList *)
{
    /* Append everything that was created last tick */
    this->items.MoveFrom(this->newly_created_items);
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
    this->items.ForEach([position, &result](auto & harvester) {
        if (harvester.IsColliding(position))
            result = &harvester;
    });
    return result;
}
