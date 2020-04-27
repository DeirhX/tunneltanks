#include <control.h>
#ifdef _WIN32
#include <crtdbg.h>
#endif
#include <cstdlib>
#include <cstring>
#include <game.h>
#include <gamelib.h>
#include <level.h>
#include <levelgen.h>
#include <memalloc.h>
#include <random.h>
#include <tweak.h>
#include <types.h>

/* Nothing in this file uses SDL, but we still have to include SDL.h for Macs,
 * since they do some extra magic in the file WRT the main() function: */
// #include <SDL.h>
#include <iostream>

#include "exceptions.h"
#include "game_config.h"
#include "gamelib/sdl/control.h"
#include "gamelib/sdl/sdl_system.h"

int GameMain(int argc, char * argv[]);

int main(int argc, char * argv[])
{
    try
    {
        #ifdef _WIN32
        _CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);
        #endif

        return GameMain(argc, argv);
    }
    catch (GameException & ex)
    {
        std::cerr << ex.what();
        return -1;
    }
}

int GameMain(int argc, char * argv[])
{
    assert(tweak::perf::parallelism_degree > 0);

    /* Game defaults that can be overridden via command-line */
    bool is_reading_level = false;
    bool is_reading_seed = false;
    bool is_reading_file = false;

    bool is_fullscreen = false;
    bool is_debug = false;
    bool is_ai = true;
    bool follow_ai = false;

    int player_count = 2;
    Size level_size{1000, 500};
    char * level_generator_id = nullptr;
    char * level_bitmap_filename = nullptr;

    int seed = 0, manual_seed = 0;

    /* Apply command-line  */
    for (int i = 1; i < argc; i++)
    {
        if (is_reading_level)
        {
            level_generator_id = argv[i];
            is_reading_level = false;
        }
        else if (is_reading_seed)
        {
            seed = std::atoi(argv[i]);
            manual_seed = 1;
            is_reading_seed = false;
        }
        else if (is_reading_file)
        {
            level_bitmap_filename = argv[i];
            is_reading_file = false;
        }
        else if (!strcmp("--help", argv[i]))
        {
            gamelib_print("%s %s\n\n", tweak::system::WindowTitle, tweak::system::Version);

            gamelib_print("--version          Display version, and exit.\n");
            gamelib_print("--help             Display this help message and exit.\n\n");

            gamelib_print("--single           Only have one user-controlled tank.\n");
            gamelib_print("--double           Have two user-controlled tanks. (Default)\n");
            gamelib_print("--follow-ai         Have view of AI opponents \n");
            gamelib_print("--no-ai            No AI-controlled players.\n\n");

            gamelib_print("--show-levels      List all available level generators.\n");
            gamelib_print("--level <GEN>      Use <GEN> as the level generator.\n");
            gamelib_print("--seed <INT>       Use <INT> as the random seed.\n");
            gamelib_print("--large            Generate a far larger level.\n");
            gamelib_print("--fullscreen       Start in fullscreen mode.\n\n");
            gamelib_print("--only-gen <FILE>  Will only write the level to a .bmp file, and exit.\n");
            gamelib_print("--debug            Write before/after .bmp's to current directory.\n");

            return 0;
        }
        else if (!strcmp("--version", argv[i]))
        {
            gamelib_print("%s %s\n", tweak::system::WindowTitle, tweak::system::Version);
            return 0;
        }
        else if (!strcmp("--single", argv[i]))
        {
            player_count = 1;
        }
        else if (!strcmp("--double", argv[i]))
        {
            player_count = 2;
        }
        else if (!strcmp("--no-ai", argv[i]))
        {
            is_ai = false;
        }
        else if (!strcmp("--follow-ai", argv[i]))
        {
            follow_ai = true;
            player_count = 1;
        }
        else if (!strcmp("--show-levels", argv[i]))
        {
            levelgen::LevelGenerator::PrintAllGenerators(stdout);
            return 0;
        }
        else if (!strcmp("--level", argv[i]))
        {
            is_reading_level = true;
        }
        else if (!strcmp("--seed", argv[i]))
        {
            is_reading_seed = true;
        }
        else if (!strcmp("--large", argv[i]))
        {
            level_size.x = 1500;
            level_size.y = 750;
        }
        else if (!strcmp("--fullscreen", argv[i]))
        {
            is_fullscreen = true;
        }
        else if (!strcmp("--only-gen", argv[i]))
        {
            is_reading_file = true;
        }
        else if (!strcmp("--debug", argv[i]))
        {
            is_debug = true;
        }
        else
        {
            gamelib_error("Unexpected argument: '%s'\n", argv[i]);
            exit(1);
        }
    }

    /* Seed if necessary: */
    if (manual_seed)
        Random.Seed(seed);
    else
        Random.Seed();

    try
    {
        /* If we're only writing the generated level to file, then just do that: */
        if (level_bitmap_filename)
        {
            /* Generate our random level: */
            levelgen::GeneratedLevel generated_level = levelgen::LevelGenerator::Generate(levelgen::LevelGenerator::FromName(level_generator_id), level_size );
            generated_level.level->MaterializeLevelTerrainAndBases();

            /* Dump it out, and exit: */
            generated_level.level->DumpBitmap(level_bitmap_filename);

            gamelib_exit();
            return 0;
        }
    }
    catch (const GameException & game_ex)
    {
        gamelib_error("Failed to dump level bitmap: %s", game_ex.what());
    }

    try
    {
        /* Let's get this ball rolling: */
        gamelib_init();

        auto config = GameConfig{
            .video_config =
                {
                    .resolution = tweak::screen::WindowSize,
                    .is_fullscreen = is_fullscreen,
                    .render_surface_size = tweak::screen::RenderSurfaceSize,
                },
            .level_size = level_size,
            .level_generator = levelgen::LevelGenerator::FromName(level_generator_id),
            .is_debug = is_debug,
            .player_count = player_count,
            .use_ai = is_ai,
            .follow_ai = follow_ai,
        };

        /* TODO: Unify this global mess */
        /* Setup input/output system */
        ::global_game_system = CreateGameSystem(config.video_config);
        ::global_game = std::make_unique<Game>(config);
        /* Play the game: */
        ::global_game->BeginGame();
        gamelib_main_loop([]() -> bool { return global_game->AdvanceStep(); });

        /* Release global resources earlier than atexit global teardown*/
        ::global_game->ClearWorld();
        ::global_game.reset();
        ::global_game_system.reset();

        /* Ok, we're done. Tear everything up: */
        gamelib_exit();
    }
    catch (const GameInitException & init_ex)
    {
        gamelib_error("Game failed to initialize due to: %s %s", init_ex.what(), init_ex.error_string);
    }
    catch (const GameException & game_ex)
    {
        gamelib_error("Game terminated with exception: %s", game_ex.what());
    }

    print_mem_stats();

    return 0;
}
