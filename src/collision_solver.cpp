#include "collision_solver.h"

Tank * CollisionSolver::TestTank(Position world_position) const
{
    return this->tank_list->GetTankAtPoint(world_position);
}

Machine * CollisionSolver::TestMachine(Position world_position) const
{
    return this->machine_list->GetMachineAtPoint(world_position);
}

LevelPixel CollisionSolver::TestTerrain(Position world_position) const
{
    return level->GetPixel(world_position);
}

MachineTemplate * CollisionSolver::TestMachineTemplate(Position world_position) const
{
    return this->machine_list->GetMachineTemplateAtPoint(world_position);
}
