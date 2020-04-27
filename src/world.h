#pragma once

#include "collision_solver.h"
#include "game.h"
#include "level.h"
#include "link.h"
#include "machine_list.h"
#include "projectile_list.h"
#include "sprite_list.h"
#include "tank_list.h"

class WorldRenderSurface;

class World
{
    class Game * game;
    int advance_count = 0;
    std::chrono::microseconds time_elapsed = {};

    std::unique_ptr<Level> level;
    LinkMap link_map;

    ProjectileList projectile_list;
    MachineryList harvester_list;
    TankList tank_list;
    SpriteList sprite_list;

    CollisionSolver collision_solver;
    RepetitiveTimer regrow_timer{tweak::world::DirtRecoverInterval};

  public:
    World(Game * game, std::unique_ptr<Level> && level);
    void Clear(); /* Clear the world of everything */

    void BeginGame();
    void Advance();
    void Draw(WorldRenderSurface * objects_surface);

    [[nodiscard]] Level * GetLevel() { return this->level.get(); }
    [[nodiscard]] ProjectileList * GetProjectileList() { return &this->projectile_list; }
    [[nodiscard]] MachineryList * GetHarvesterList() { return &this->harvester_list; }
    [[nodiscard]] TankList * GetTankList() { return &this->tank_list; }
    [[nodiscard]] SpriteList * GetSpriteList() { return &this->sprite_list; }
    [[nodiscard]] LinkMap * GetLinkMap() { return &this->link_map; }
    [[nodiscard]] const CollisionSolver * GetCollisionSolver() const { return &this->collision_solver; }
    [[nodiscard]] std::chrono::microseconds GetElapsedTime() const { return this->time_elapsed; }

    void SetGameOver();

  private:
    std::chrono::microseconds regrow_elapsed = {};
    std::chrono::microseconds regrow_average = {};

    /* Attempts to regrow destroyed dirt in empty places where there is some neighboring dirt to extend */
    void RegrowPass();
};

inline World * GetWorld() { return GetGame()->GetWorld(); }
