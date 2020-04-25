#pragma once
#include "machine_list.h"
#include "tank_list.h"

class CollisionSolver
{
    Level * level;
    TankList * tank_list;
    MachineryList * machine_list;
  public:
    CollisionSolver(Level * level, TankList * tank_list, MachineryList * harvester_list)
        : level(level), tank_list(tank_list), machine_list(harvester_list)
    { }

    /* Root types */
    Tank * TestTank(Position world_position) const;
    Machine * TestMachine(Position world_position) const;
    LevelPixel TestTerrain(Position world_position) const;

    /* Specialized types, included in root types*/
    MachineTemplate * TestMachineTemplate(Position world_position) const;


    template <typename TankCollideFunc, typename HarvesterCollideFunc, typename TerrainCollideFunc>
    bool TestCollide(Position world_position, TankCollideFunc tank_collide, HarvesterCollideFunc machine_collide,
                 TerrainCollideFunc terrain_collide) const;
};

/* Return true from collision functions if you registered the collision */
template <typename TankCollideFunc, typename MachineCollideFunc, typename TerrainCollideFunc>
bool CollisionSolver::TestCollide(Position world_position, TankCollideFunc tank_collide,
                              MachineCollideFunc machine_collide, TerrainCollideFunc terrain_collide) const
{
    bool collided = false;
    auto tank_result = TestTank(world_position);
    if (tank_result)
        collided = collided || tank_collide(*tank_result);
    auto machine_result = TestMachine(world_position);
    if (machine_result)
        collided = collided || machine_collide(*machine_result);
    auto terrain_result = TestTerrain(world_position);
    if (terrain_result != LevelPixel::Blank)
        collided = collided || terrain_collide(terrain_result);
    return collided;
}
