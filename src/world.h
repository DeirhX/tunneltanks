#pragma once

#include "collision_solver.h"
#include "game.h"
#include "terrain.h"
#include "link.h"
#include "machine_list.h"
#include "projectile_list.h"
#include "sprite_list.h"
#include "tank_list.h"
#include "entity.h"
#include "world_sector.h"

namespace crust
{
class WorldRenderSurface;

class World
{
    class Game * game;
    int advance_count = 0;
    std::chrono::microseconds time_elapsed = {};

    Terrain terrain;
    TankBases tank_bases;
    /* Powerups */
    /* Enemy spawns */
    /* Hazards */

    LinkMap link_map;

    ProjectileList projectile_list;
    MachineryList harvester_list;
    TankList tank_list;
    SpriteList sprite_list;

    CollisionSolver collision_solver;
    RepetitiveTimer regrow_timer{tweak::world::DirtRecoverInterval};

    crust::WorldSectors sectors;

    //EntitySystem entities;
  public:
    World(Size terrain_size);
    void Clear(); /* Clear the world of everything */

    void BeginGame(class Game * game);
    void Advance();
    void Draw(WorldRenderSurface & objects_surface);

    [[nodiscard]] Terrain & GetTerrain() { return this->terrain; }
    [[nodiscard]] TankBases & GetTankBases() { return this->tank_bases; }
    [[nodiscard]] ProjectileList & GetProjectileList() { return this->projectile_list; }
    [[nodiscard]] MachineryList & GetHarvesterList() { return this->harvester_list; }
    [[nodiscard]] TankList & GetTankList() { return this->tank_list; }
    [[nodiscard]] SpriteList & GetSpriteList() { return this->sprite_list; }
    [[nodiscard]] LinkMap & GetLinkMap() { return this->link_map; }
    [[nodiscard]] const CollisionSolver & GetCollisionSolver() const { return this->collision_solver; }
    [[nodiscard]] std::chrono::microseconds GetElapsedTime() const { return this->time_elapsed; }
    [[nodiscard]] WorldSectors & GetSectors() { return this->sectors; }

    void SetGameOver();

    struct SimulationProfile
    {
        std::chrono::microseconds regrow{};
        std::chrono::microseconds projectiles{};
        std::chrono::microseconds tanks{};
        std::chrono::microseconds harvesters{};
        std::chrono::microseconds sprites{};
        std::chrono::microseconds bases{};
        std::chrono::microseconds links{};
        std::chrono::microseconds terrain_advance{};
        std::chrono::microseconds ecs{};
        std::chrono::microseconds total{};
        int frame_count = 0;

        void Reset() { *this = {}; }
    };

    [[nodiscard]] const SimulationProfile & GetProfile() const { return profile; }

  private:
    SimulationProfile profile{};
    static constexpr int ProfileReportInterval = 100;

    std::chrono::microseconds regrow_elapsed = {};
    std::chrono::microseconds regrow_average = {};

    void RegrowPass();
    void ReportProfile();
};

inline World * GetWorld() { return GetGame()->GetWorld(); }
} // namespace MyNamespace