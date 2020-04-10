#pragma once

#include "collision_solver.h"
#include "harvester_list.h"
#include "level.h"
#include "projectile_list.h"
#include "tanklist.h"

class World
{
    class Game * game;

    std::unique_ptr<Level> level;
    std::unique_ptr<ProjectileList> projectile_list;
    std::unique_ptr<HarvesterList> harvester_list;
    std::unique_ptr<TankList> tank_list;

    CollisionSolver collision_solver;
    std::chrono::microseconds regrow_timer = tweak::world::DirtRecoverInterval;
  public:
    World(Game * game, std::unique_ptr<Level> && level);
    void Advance(class WorldRenderSurface * objects_surface);

    TankList * GetTankList() { return this->tank_list.get(); }
    ProjectileList * GetProjectileList() { return this->projectile_list.get(); }
    HarvesterList * GetHarvesterList() { return this->harvester_list.get(); }
    Level * GetLevel() { return this->level.get(); }
    const CollisionSolver * GetCollisionSolver() const { return &this->collision_solver; }
    void GameIsOver();

  private:
    std::chrono::microseconds regrow_elapsed = {};
    std::chrono::microseconds regrow_average = {};
    int advance_count = 0;

    /* Attempts to regrow destroyed dirt in empty places where there is some neighboring dirt to extend */
    void RegrowPass();
};
