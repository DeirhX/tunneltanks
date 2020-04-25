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
        if (harvester.TestCollide(position))
            result = &harvester;
    });
    return result;
}

MachineTemplate * MachineryList::GetMachineTemplateAtPoint(Position position)
{
    /* TODO: implement correctly */
    assert(!"Not implemented");
    return nullptr;

    /*MachineTemplate * result = nullptr;
    this->items.ForEach<MachineTemplate>([position, &result](auto & harvester) {
        if (harvester.TestCollide(position))
            result = &harvester;
    });
    return result;*/
}
