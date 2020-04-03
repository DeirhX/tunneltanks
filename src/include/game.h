#pragma once
#include "levelgen.h"
#include <cassert>
#include <memory>
#include <types.h>

#include "game_config.h"

class TankList;

class Game
{
    int is_active = false;
    int is_debug = false;

    GameConfig config = {};

    std::unique_ptr<class LevelPixelSurface> draw_buffer;
    std::unique_ptr<class Screen> screen;
    std::unique_ptr<class World> world;

    std::unique_ptr<class GameMode> mode;

  public:
    Game(GameConfig config);
    ~Game();

    bool AdvanceStep();
    void GameOver();

    World * GetWorld() { return world.get(); }
  private:
};

inline std::unique_ptr<Game> global_game;
inline Game * GetGame() { return global_game.get(); }
inline World * GetWorld() { return GetGame()->GetWorld(); }

class GameMode
{
  protected:
    Screen * screen;
    World * world;

  public:
    GameMode(Screen * screen, World * world) : screen(screen), world(world) {}
    ~GameMode() { assert(!screen && !world || !"Didn't tear it down"); }

    virtual void TearDown() = 0;

    static void AssumeAIControl(TankList * tank_list, Level * level, TankColor starting_id);
};

class SinglePlayerMode : public GameMode
{
  private:
    SinglePlayerMode(Screen * screen, World * world) : GameMode(screen, world) {}

  public:
    static std::unique_ptr<SinglePlayerMode> Setup(Screen * screen, World * world, bool use_ai);
    void TearDown() override;
};

class LocalTwoPlayerMode : public GameMode
{
  private:
    LocalTwoPlayerMode(Screen * screen, World * world) : GameMode(screen, world) {}

  public:
    static std::unique_ptr<LocalTwoPlayerMode> Setup(Screen * screen, World * world, bool use_ai);
    void TearDown() override;
};
