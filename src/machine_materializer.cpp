#include "machine_materializer.h"
#include "controller.h"
#include "game.h"
#include "types.h"
#include "tank.h"
#include "world.h"

MachineMaterializer::MachineMaterializer(Tank * tank, MaterialContainer * resource_bank)
    : owner_tank(tank), resource_bank(resource_bank)
{

}

void MachineMaterializer::PickUpMachine(Machine & machine)
{ this->transported_machine = &machine; }

void MachineMaterializer::PlaceMachine()
{
    assert(this->transported_machine);
    if (this->transported_machine)
    {
        /* Finally set the position */
        this->transported_machine->SetPosition(this->owner_tank->GetPosition());
        this->transported_machine->SetState(MachineConstructState::Planted);
        /* Stop transporting */
        this->transported_machine = nullptr;
    }
    
}

void MachineMaterializer::ApplyControllerOutput(ControllerOutput controls)
{
    this->is_building_primary = controls.build_primary;
    this->is_building_secondary = controls.build_secondary;
    this->is_building_tertiary = controls.build_tertiary;
}

void MachineMaterializer::Advance(Position)
{
    if (this->is_building_primary)
    {
        if (!this->transported_machine)
        {
            /* Pick up a machine if we're not holding it */
            Machine * machine_overlap = nullptr;
            this->owner_tank->ForEachTankPixel([&machine_overlap](Position world_position) {
                machine_overlap = GetWorld()->GetCollisionSolver()->TestMachine(world_position);
                return !machine_overlap;
            });
            if (machine_overlap)
            {
                PickUpMachine(*machine_overlap);
            }
        }
        else
        {
            /* We're holding a machine, Place a machine if we were - but not in any base */
            if (!GetWorld()->GetLevel()->CheckBaseCollision(this->owner_tank->GetPosition()))
            {
                PlaceMachine();
            }
            else
            {
                /* Return a machine template to its place if we were holding it */
                MachineTemplate * machine_template_overlap = nullptr;
                this->owner_tank->ForEachTankPixel([&machine_template_overlap](Position world_position) {
                    Machine * machine_overlap = GetWorld()->GetCollisionSolver()->TestMachine(world_position);
                    auto * as_machine_template = dynamic_cast<MachineTemplate *>(machine_overlap);
                    if (as_machine_template)
                        machine_template_overlap = as_machine_template;
                    return !machine_template_overlap;
                });
                if (machine_template_overlap == this->transported_machine)
                {
                    machine_template_overlap->ResetToOrigin(); 
                    this->transported_machine = nullptr;
                }
            }
        }
    }
    /*
    else if (this->is_building_secondary)
        TryBuildMachine(this->secondary_construct);
    else if (this->is_building_tertiary)
        TryBuildMachine(this->tertiary_construct);
        */

    if (this->transported_machine)
    {
        this->transported_machine->SetPosition(this->owner_tank->GetPosition());
    }
}

bool MachineMaterializer::TryBuildMachine(MachineType type)
{
    Position position = (this->owner_tank->GetTurretBarrel() + this->owner_tank->GetTurretDirection() * 3.f).ToIntPosition();

    switch (type)
    {
    case MachineType::Harvester:
        if (this->resource_bank->Pay(tweak::rules::HarvesterCost))
        {
            GetWorld()->GetHarvesterList()->Emplace<Harvester>(position, HarvesterType::Dirt, this->owner_tank);
            return true;
        }
        break;
    case MachineType::Miner:
        if (this->resource_bank->Pay(tweak::rules::MinerCost))
        {
            GetWorld()->GetHarvesterList()->Emplace<Harvester>(position, HarvesterType::Mineral, this->owner_tank);
            return true;
        }
        break;
    case MachineType::Charger:
        if (this->resource_bank->Pay(tweak::rules::ChargerCost))
        {
            GetWorld()->GetHarvesterList()->Emplace<Charger>(position, this->owner_tank);
            return true;
        }
        break;
    default:
        assert(!"Unknown type");
    }
    return false;
}
