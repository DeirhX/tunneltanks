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
{
    this->transported_machine = &machine;
}

void MachineMaterializer::PickUpMachine(MachineTemplate & machine_template)
{
    this->transported_machine = &machine_template.BuildMachine();
    this->transported_machine->SetIsTransported(true);
}

void MachineMaterializer::PlaceMachine()
{
    assert(this->transported_machine);
    if (this->transported_machine)
    {
        this->transported_machine->SetIsTransported(false);
        this->transported_machine->SetPosition(this->owner_tank->GetPosition());
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
        /* Not holding anything ? */
        if (!this->transported_machine)
        {
            /* Pick up a machine if we're not holding it */
            MachineTemplate * machine_template_overlap = nullptr;
            this->owner_tank->ForEachTankPixel([&machine_template_overlap](Position world_position) {
                machine_template_overlap = GetWorld()->GetCollisionSolver()->TestMachineTemplate(world_position);
                return !machine_template_overlap;
            });
            if (machine_template_overlap)
            {
                PickUpMachine(*machine_template_overlap);
            }
        }
        /* Holding a machine */
        else
        {
            /* Don't place it if it overlaps with any other machine */
            Machine * machine_overlap = nullptr;
            this->owner_tank->ForEachTankPixel([&machine_overlap](Position world_position) {
                machine_overlap = GetWorld()->GetCollisionSolver()->TestMachine(world_position);
                return !machine_overlap;
            });

            if (!machine_overlap)
            {                
                PlaceMachine();
            }
        }
    }

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
