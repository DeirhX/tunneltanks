#include <SDL.h>
#include <ctime>

#include <gamelib.h>
#include <tank.h>
#include <tweak.h>

#include "control.h"
#include "controllersdl.h"
#include "exceptions.h"
#include "require_sdl.h"

/* Set up SDL: */
int gamelib_init()
{
    char text[1024];

    if (SDL_Init(SDL_INIT_EVERYTHING) < 0)
    {
        gamelib_error("Failed to initialize SDL: %s\n", SDL_GetError());
        return 1;
    }

    /* Dump out the current graphics driver, just for kicks: */
    SDL_VideoDriverName(text, sizeof(text));
    gamelib_print("Using video driver: %s\n", text);

    return 0;
}

/* Frees stuff up: */
int gamelib_exit()
{
    SDL_Quit();
    return 0;
}

/* Waits long enough to maintain a consistent FPS: */
void smart_wait()
{
    int cur, next;

    /* Get the current time, and the next time: */
    cur = SDL_GetTicks();
    next = int((cur / tweak::perf::AdvanceStep.count() + 1) * tweak::perf::AdvanceStep.count());

    /* Wait if we need to: */
    if (cur >= next)
        return;
    SDL_Delay(next - cur);
}

/*
void gamelib_handle_fps() {
	frames += 1;
	newtiempo = time(NULL);
	if(newtiempo != tiempo) {
		char buffer[50];
		sprintf(buffer, "%s %s (%u fps)", WINDOW_TITLE, VERSION, frames);
		SDL_WM_SetCaption(buffer, buffer);
		frames = 0;
		tiempo = newtiempo;
	}
}
*/

/* All of this backend's capabilities: */
int gamelib_get_max_players() { return 2; }
bool gamelib_get_can_resize() { return 1; }
bool gamelib_get_can_fullscreen() { return 1; }
bool gamelib_get_can_window() { return 1; }
int gamelib_get_target_fps() { return tweak::perf::TargetFps; }

static bool try_attach_gamepad(Tank * tank, int gamepad_num)
{
    if (SDL_NumJoysticks() < gamepad_num)
        return false;
    try
    {
        tank->SetController(std::make_shared<GamePadController>());
        gamelib_print("Using Joystick #%d for player %d\n", gamepad_num, int(tank->GetColor()));
    }
    catch (const GameException & ex)
    {
        gamelib_print("Failed to use joystick #%d: %s\n", gamepad_num, ex.what());
    }
    return true;
}

#define ONE_KEYBOARD SDLK_LEFT, SDLK_RIGHT, SDLK_UP, SDLK_DOWN, SDLK_LCTRL
#define TWO_KEYBOARD_A SDLK_a, SDLK_d, SDLK_w, SDLK_s, SDLK_LCTRL
#define TWO_KEYBOARD_B SDLK_LEFT, SDLK_RIGHT, SDLK_UP, SDLK_DOWN, SDLK_SLASH

void gamelib_tank_attach(Tank * tank, int tank_num, int num_players)
{
    if (num_players == 1 && tank_num == 0)
    {
        if (!try_attach_gamepad(tank, tank_num))
            tank->SetController(std::make_shared<KeyboardWithMouseController>(ONE_KEYBOARD));
    }
    else if (num_players == 2)
    {
        if (tank_num == 0)
        {
            if (!try_attach_gamepad(tank, tank_num))
                tank->SetController(std::make_shared<KeyboardController>(TWO_KEYBOARD_A));
        }
        else if (tank_num == 1)
        {
            if (!try_attach_gamepad(tank, tank_num))
            {
                //if (SDL_NumJoysticks())
                //    tank->SetController(std::make_shared<KeyboardController>(ONE_KEYBOARD));
                //else
                tank->SetController(std::make_shared<KeyboardController>(TWO_KEYBOARD_B));
            }
        }
    }
}
