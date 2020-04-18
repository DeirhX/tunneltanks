#pragma once

#include "collision_solver.h"
#include "game.h"
#include "machine_list.h"
#include "level.h"
#include "link.h"
#include "projectile_list.h"
#include "tank_list.h"

class WorldRenderSurface;

class World
{
    class Game * game;

    std::unique_ptr<Level> level;
    LinkMap link_map;
    ProjectileList projectile_list;
    MachineryList harvester_list;
    TankList tank_list;

    CollisionSolver collision_solver;
    RepetitiveTimer regrow_timer{tweak::world::DirtRecoverInterval};
  public:
    World(Game * game, std::unique_ptr<Level> && level);
    void Clear(); /* Clear the world of everything */

    void Advance();
    void Draw(WorldRenderSurface * objects_surface);

    Level * GetLevel() { return this->level.get(); }
    ProjectileList * GetProjectileList() { return &this->projectile_list; }
    MachineryList * GetHarvesterList() { return &this->harvester_list; }
    TankList * GetTankList() { return &this->tank_list; }
    LinkMap * GetLinkMap() { return &this->link_map; }
    const CollisionSolver * GetCollisionSolver() const { return &this->collision_solver; }

    void GameIsOver();

  private:
    std::chrono::microseconds regrow_elapsed = {};
    std::chrono::microseconds regrow_average = {};
    int advance_count = 0;

    /* Attempts to regrow destroyed dirt in empty places where there is some neighboring dirt to extend */
    void RegrowPass();
};

inline World * GetWorld() { return GetGame()->GetWorld(); }
