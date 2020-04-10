#include "machine_materializer.h"
#include "controller.h"
#include "game.h"
#include "types.h"
#include "tank.h"
#include "world.h"

MachineMaterializer::MachineMaterializer(Tank * tank, Resources * resource_bank) : owner_tank(tank), resource_bank(resource_bank)
{

}

void MachineMaterializer::ApplyControllerOutput(ControllerOutput controls)
{
    if (controls.build_primary)
        this->is_building_primary = true;
    else if (controls.build_secondary)
        this->is_building_secondary = true;
}

void MachineMaterializer::Advance(Position tank_position, widgets::Crosshair * crosshair)
{
    if (this->is_building_primary)
        TryBuildMachine(this->primary_construct);
    else if (this->is_building_secondary)
        TryBuildMachine(this->secondary_construct);
}

bool MachineMaterializer::TryBuildMachine(MachineType type)
{
    switch (type)
    {
    case MachineType::Harvester:
        if (this->resource_bank->PayMinerals(tweak::rules::HarvesterDirtCost))
        {
            GetWorld()->GetHarvesterList()->Add(Harvester(this->owner_tank->GetPosition(), HarvesterType::Dirt));
            return true;
        }
        break;
    case MachineType::Miner:
        if (this->resource_bank->PayMinerals(tweak::rules::MinerDirtCost))
        {
            GetWorld()->GetHarvesterList()->Add(Harvester(this->owner_tank->GetPosition(), HarvesterType::Mineral));
            return true;
        }
        break;
    default:
        assert(!"Unknown type");
    }
    return false;
}
