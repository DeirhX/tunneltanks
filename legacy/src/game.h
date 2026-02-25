#pragma once
#include <cassert>
#include <memory>
#include <types.h>
#include "game_config.h"
#include "entity.h"

namespace crust
{

class TankBases;
class TankList;

class Game
{
    bool is_active = false;

    GameConfig config = {};

    std::unique_ptr<class Screen> screen;
    std::unique_ptr<class World> world;
    std::unique_ptr<class GameMode> mode;

    struct DrawProfile
    {
        std::chrono::microseconds terrain_draw{};
        std::chrono::microseconds objects_draw{};
        std::chrono::microseconds screen_draw{};
        std::chrono::microseconds total_frame{};
        int frame_count = 0;

        void Reset() { *this = {}; }
    } draw_profile{};

    static constexpr int ProfileReportInterval = 100;
    void ReportDrawProfile();

  public:
    Game(GameConfig config);
    ~Game();

    void BeginGame(class Renderer * renderer);
    bool AdvanceStep();
    void GameOver();

    void ClearWorld();
    World * GetWorld() { return world.get(); }
};

inline EntitySystem entity_system{}; // Needs to live longer than World as it might have raw pointers into it
inline std::unique_ptr<Game> global_game;

inline Game * GetGame()
{
    assert(global_game.get());
    return global_game.get();
}

inline ecs::registry & entities() { return entity_system.registry; }
inline const ecs::registry & const_entities() { return entity_system.registry; }

class GameMode
{
  protected:
    Screen * screen;
    World * world;

  public:
    GameMode(Screen * screen, World * world) : screen(screen), world(world) {}
    virtual ~GameMode() { assert(!screen && !world || !"Didn't tear it down"); }

    virtual void TearDown();

    static void SpawnAIOpponents(TankList * tank_list, TankBases * bases, TankColor starting_id, int spawn_amount);
    static void AssumeAIControl(class Tank * tank);
};

class SinglePlayerMode : public GameMode
{
  public:
    SinglePlayerMode(Screen * screen, World * world) : GameMode(screen, world) {}

  public:
    static std::unique_ptr<SinglePlayerMode> Setup(Screen * screen, World * world, bool use_ai);
};

class FollowAISinglePlayerMode : public GameMode
{
  public:
    FollowAISinglePlayerMode(Screen * screen, World * world) : GameMode(screen, world) {}

  public:
    static std::unique_ptr<FollowAISinglePlayerMode> Setup(Screen * screen, World * world);
};

class LocalTwoPlayerMode : public GameMode
{
  public:
    LocalTwoPlayerMode(Screen * screen, World * world) : GameMode(screen, world) {}

  public:
    static std::unique_ptr<LocalTwoPlayerMode> Setup(Screen * screen, World * world, bool use_ai);
};

} // namespace MyNamespace