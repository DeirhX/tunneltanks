#pragma once

class Resources;
class Tank;
struct Position;
struct ControllerOutput;
namespace widgets {class Crosshair;}

enum class MachineType
{
    Harvester,
    Miner,
};

class MachineMaterializer
{
    MachineType primary_construct = MachineType::Harvester;
    MachineType secondary_construct = MachineType::Miner;

    bool is_building_primary = false;
    bool is_building_secondary = false;

    Tank * owner_tank;
    Resources * resource_bank;
  public:
    MachineMaterializer(Tank * owner_tank, Resources * resource_bank);

    void ApplyControllerOutput(ControllerOutput controls);
    void Advance(Position tank_position, widgets::Crosshair * crosshair);

private:
    bool TryBuildMachine(MachineType type);
};
