#pragma once

class Materials;
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

    bool is_building_primary = false;
    bool is_building_secondary = false;
    bool is_building_tertiary = false;

    Tank * owner_tank;
    Materials * resource_bank;
  public:
    MachineMaterializer(Tank * owner_tank, Materials * resource_bank);

    void ApplyControllerOutput(ControllerOutput controls);
    void Advance(Position tank_position);

private:
    bool TryBuildMachine(MachineType type);
};
