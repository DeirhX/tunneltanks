#pragma once
#include "harvester_list.h"
#include "tanklist.h"

class CollisionSolver
{
    Level * level;
    TankList * tank_list;
    HarvesterList * harvester_list;
  public:
    CollisionSolver(Level * level, TankList * tank_list, HarvesterList * harvester_list)
        : level(level), tank_list(tank_list), harvester_list(harvester_list)
    { }

    Tank * TestTank(Position world_position) const;
    Harvester * TestHarvester(Position world_position) const;
    LevelPixel TestTerrain(Position world_position) const;
    template <typename TankCollideFunc, typename HarvesterCollideFunc, typename TerrainCollideFunc>
    bool TestCollide(Position world_position, TankCollideFunc tank_collide, HarvesterCollideFunc harvester_collide,
                 TerrainCollideFunc terrain_collide) const;
};

template <typename TankCollideFunc, typename HarvesterCollideFunc, typename TerrainCollideFunc>
bool CollisionSolver::TestCollide(Position world_position, TankCollideFunc tank_collide,
                              HarvesterCollideFunc harvester_collide, TerrainCollideFunc terrain_collide) const
{
    bool collided = false;
    auto tank_result = TestTank(world_position);
    if (tank_result)
        collided = collided || tank_collide(*tank_result);
    auto harvester_result = TestHarvester(world_position);
    if (harvester_result)
        collided = collided || harvester_collide(*harvester_result);
    auto terrain_result = TestTerrain(world_position);
    if (terrain_result != LevelPixel::Blank)
        collided = collided || terrain_collide(terrain_result);
    return collided;
}
