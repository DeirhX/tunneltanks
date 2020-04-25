#pragma once

class Machine;
class MaterialContainer;
class Tank;
struct Position;
struct ControllerOutput;
namespace widgets {class Crosshair;}

enum class MachineType
{
    Harvester,
    Miner,
    Charger,
};

class MachineMaterializer
{
    MachineType primary_construct = MachineType::Harvester;
    MachineType secondary_construct = MachineType::Miner;
    MachineType tertiary_construct = MachineType::Charger;

    //bool build_primary = false;

    bool is_building_primary = false;
    bool is_building_secondary = false;
    bool is_building_tertiary = false;

    Tank * owner_tank = nullptr;
    Machine * transported_machine = nullptr;
    MaterialContainer * resource_bank = nullptr;
  public:
    MachineMaterializer(Tank * owner_tank, MaterialContainer * resource_bank);

    void PickUpMachine(Machine & machine);
    void PlaceMachine();

    void ApplyControllerOutput(ControllerOutput controls);
    void Advance(Position tank_position);

private:
    bool TryBuildMachine(MachineType type);
};
