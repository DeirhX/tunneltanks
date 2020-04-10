#include "collision_solver.h"

Tank * CollisionSolver::TestTank(Position world_position) const
{
    return this->tank_list->GetTankAtPoint(world_position);
}

Harvester * CollisionSolver::TestHarvester(Position world_position) const
{
    return this->harvester_list->GetHarvesterAtPoint(world_position);
}

LevelPixel CollisionSolver::TestTerrain(Position world_position) const
{
    return level->GetPixel(world_position);
}
