#include "machine_list.h"

void MachineryList::Advance(Terrain * level, TankList *)
{
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
    MachineTemplate * result = nullptr;
    this->items.ForEachConvertibleTo<MachineTemplate>([position, &result](auto & harvester) {
        if (harvester.TestCollide(position))
            result = &harvester;
    });
    return result;
}
