#pragma once
#include "item_list_adaptor.h"
#include "containers.h"
#include "machine.h"

class MachineryList : public ItemListAdaptor<Harvester, Charger, HarvesterTemplate, ChargerTemplate>
{
public:
    void Advance(class Terrain * level, class TankList * tank_list);
    void Draw(class Surface * surface);

    Machine * GetMachineAtPoint(Position position);
    MachineTemplate * GetMachineTemplateAtPoint(Position position);
};
