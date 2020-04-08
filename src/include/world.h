#pragma once

#include "level.h"
#include "projectile_list.h"
#include "tanklist.h"

class World
{
    class Game * game;

    std::unique_ptr<TankList> tank_list;
    std::unique_ptr<ProjectileList> projectile_list;
    std::unique_ptr<Level> level;

  public:
    World(Game * game, 
          std::unique_ptr<TankList> && tank_list, 
          std::unique_ptr<ProjectileList> && projectile_list,
          std::unique_ptr<Level> && level)
        : game(game), tank_list(std::move(tank_list)), projectile_list(std::move(projectile_list)),
          level(std::move(level))
    {
    }
    void Advance(class WorldRenderSurface * objects_surface);

    TankList * GetTankList() { return this->tank_list.get(); }
    ProjectileList * GetProjectileList() { return this->projectile_list.get(); }
    Level * GetLevel() { return this->level.get(); }
    void GameIsOver();

  private:
    std::chrono::microseconds regrow_elapsed = {};
    std::chrono::microseconds regrow_average = {};
    int advance_count = 0;

    /* Attempts to regrow destroyed dirt in empty places where there is some neighboring dirt to extend */
    void RegrowPass();
};
