#include "game.h"
#include "ai_controller.h"
#include "bitmaps.h"
#include "exceptions.h"
#include "game_system.h"
#include "gamelib.h"
#include "level.h"
#include "levelgen.h"
#include "screen.h"
#include "tank_list.h"
#include "tweak.h"
#include "twitch_ai.h"
#include "world.h"
#include <chrono>

/*----------------------------------------------------------------------------*
 * This bit is used to initialize various GUIs:                               *
 *----------------------------------------------------------------------------*/

void GameMode::SpawnAIOpponents(TankList * tank_list, Level * level, TankColor starting_id, int spawn_amount)
{
    for (TankColor i = starting_id; (i < tweak::world::MaxPlayers) && (i - starting_id < spawn_amount); i++)
    {
        Tank * tank = tank_list->AddTank(i, level->GetSpawn(i));
        tank->SetController(std::make_shared<AiController<TwitchAI>>());
    }
}

void GameMode::AssumeAIControl(Tank * tank)
{ tank->SetController(std::make_shared<AiController<TwitchAI>>()); }

void GameMode::TearDown()
{
    this->screen->ClearGuiElements();
    this->screen = nullptr;
    this->world = nullptr;
}

std::unique_ptr<SinglePlayerMode> SinglePlayerMode::Setup(Screen * screen, World * world, bool use_ai)
{

    /* Ready the tank! */
    Tank * player = world->GetTankList()->AddTank(0, world->GetLevel()->GetSpawn(0));
    gamelib_tank_attach(player, 0, 1);

    Screens::SinglePlayerScreenSetup(*screen, *player);

    /* Fill up the rest of the slots with Twitches: */
    if (use_ai)
        GameMode::SpawnAIOpponents(world->GetTankList(), world->GetLevel(), 1, tweak::world::MaxPlayers - 1);

    return std::make_unique<SinglePlayerMode>(screen, world);
}


std::unique_ptr<FollowAISinglePlayerMode> FollowAISinglePlayerMode::Setup(Screen * screen, World * world)
{
    /* Ready the tanks! */
    Tank * player_one = world->GetTankList()->AddTank(0, world->GetLevel()->GetSpawn(0));
    gamelib_tank_attach(player_one, 0, 2);

    /* Load up two controllable tanks: */
    Tank * player_two = world->GetTankList()->AddTank(1, world->GetLevel()->GetSpawn(1));
    /*controller_twitch_attach(t);  << Attach a twitch to a camera tank, so we can see if they're getting smarter... */
    GameMode::AssumeAIControl(player_two);

    Screens::AIViewSinglePlayerSetup(*screen, *player_one, *player_two);
    /* Fill up the rest of the slots with Twitches: */

    return std::make_unique<FollowAISinglePlayerMode>(screen, world);
}

std::unique_ptr<LocalTwoPlayerMode> LocalTwoPlayerMode::Setup(Screen * screen, World * world, bool use_ai)
{
    /* Ready the tanks! */
    Tank * player_one = world->GetTankList()->AddTank(0, world->GetLevel()->GetSpawn(0));
    gamelib_tank_attach(player_one, 0, 2);

    /* Load up two controllable tanks: */
    Tank * player_two = world->GetTankList()->AddTank(1, world->GetLevel()->GetSpawn(1));
    /*controller_twitch_attach(t);  << Attach a twitch to a camera tank, so we can see if they're getting smarter... */
    gamelib_tank_attach(player_two, 1, 2);

    Screens::TwoPlayerScreenSetup(*screen,*player_one, *player_two);
    /* Fill up the rest of the slots with Twitches: */
    if (use_ai)
        GameMode::SpawnAIOpponents(world->GetTankList(), world->GetLevel(), 2, tweak::world::MaxPlayers - 2);

    return std::make_unique<LocalTwoPlayerMode>(screen, world); 
}

/* Create a default game structure: */
Game::Game(GameConfig config)
{
    /* Copy in all the default values: */
    this->config = config;
    this->is_active = false;

    if (gamelib_get_can_window())
        this->config.video_config.is_fullscreen = false;
    else if (gamelib_get_can_fullscreen())
        this->config.video_config.is_fullscreen = true;
    else
    {
        throw GameException("gamelib can't run fullscreen or in a window.");
    }

    /* Initialize screen */
    this->screen =
        std::make_unique<Screen>(this->config.video_config.is_fullscreen, GetSystem()->GetSurface());

    /* Benchmark level generation */
    int TestIterations = 20;
    #ifdef _DEBUG
        TestIterations = 1;
    #endif
    std::chrono::milliseconds time_taken = {};

    /* Generate our random level: */
    std::unique_ptr<Level> level;
    for (int i = 0; i != TestIterations; ++i)
    {
        gamelib_print("Generating level %d/%d...\n", i+1, TestIterations);
        auto generated_level = levelgen::LevelGenerator::Generate(this->config.level_generator, this->config.level_size);
        time_taken += generated_level.generation_time;
        level = std::move(generated_level.level);
    }
    auto average_time = time_taken / TestIterations;
    gamelib_print("***\r\nAverage level time: %lld.%03lld sec\n", average_time.count() / 1000,
                  average_time.count() % 1000);

    /* Create projectile list, tank list and materialize the level voxels */
    level->MaterializeLevelTerrainAndBases();

    /* Debug the starting data, if we're debugging: */
    if (this->config.is_debug)
        level->DumpBitmap("debug_start.bmp");

    /* Push the level to the draw buffer */
    level->CommitAll();
    this->screen->SetDrawLevelSurfaces(level->GetSurfaces());

    /* Create the world */
    world = std::make_unique<World>(this, std::move(level));

}

/* Step the game simulation by handling events, and drawing: */
bool Game::AdvanceStep()
{
    assert(this->is_active);

    GameEvent temp;

    /* Handle all queued events: */
    while ((temp = gamelib_event_get_type()) != GameEvent::None)
    {

        /* Trying to resize the window? */
        if (temp == GameEvent::Resize)
        {
            /* No need to do anything, we keep our surface size anyway */
        }
        /* Trying to toggle fullscreen? */
        else if (temp == GameEvent::ToggleFullscreen)
        {
            this->screen->SetFullscreen(this->screen->GetFullscreen());

        }
        /* Trying to exit? */
        else if (temp == GameEvent::Exit)
        {
            return false;
        }

        /* Done with this event: */
        gamelib_event_done();
    }

    /* Do the world advance - apply controller input, move stuff, commit level bitmap to DrawBuffer */
    /* TODO: Don't get the surface this stupid way */
    world->Advance();
    world->Draw(&this->world->GetLevel()->GetSurfaces()->objects_surface);
    /* Draw our current state */
    this->screen->DrawCurrentMode();

    return true;
}

/* Done with a game structure: */
Game::~Game()
{
    if (this->is_active)
    {
        /* Debug if we need to: */
        if (this->config.is_debug)
            world->GetLevel()->DumpBitmap("debug_end.bmp");
        this->mode->TearDown();
    }
}

void Game::BeginGame()
{
    /* Set up the players/GUI inside the world */
    if (this->config.player_count > gamelib_get_max_players())
        throw GameException("Tried to use more players than the platform can support.");

    if (this->config.follow_ai)
        this->mode = FollowAISinglePlayerMode::Setup(this->screen.get(), this->world.get());
    else if (this->config.player_count == 1)
        this->mode = SinglePlayerMode::Setup(this->screen.get(), this->world.get(), this->config.use_ai);
    else if (this->config.player_count == 2)
        this->mode = LocalTwoPlayerMode::Setup(this->screen.get(), this->world.get(), this->config.use_ai);
    else
    {
        throw GameException("Don't know how to draw more than 2 players at once...");
    }

    this->is_active = true;
    world->BeginGame();
}

void Game::GameOver() { assert(!"Implement game over!"); }

void Game::ClearWorld()
{
    world->Clear();
}

