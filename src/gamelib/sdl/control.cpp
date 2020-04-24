#include "control.h"
#include "exceptions.h"
#include "gamelib.h"
#include "require_sdl.h"
#include "sdl_controller.h"
#include "tank.h"
#include "tweak.h"
#include <SDL.h>

/* Set up SDL: */
void gamelib_init()
{
    if (SDL_Init(SDL_INIT_EVERYTHING) < 0)
    {
        throw GameInitException("Failed to initialize SDL", SDL_GetError());
    }
}

/* Frees stuff up: */
void gamelib_exit() { SDL_Quit(); }

/* Waits long enough to maintain a consistent FPS: */
void smart_wait()
{
    /* Get the current time, and the next time: */
    int64_t cur = SDL_GetTicks();
    int64_t next = (cur * 1000 / tweak::world::AdvanceStep.count() + 1) * tweak::world::AdvanceStep.count() / 1000;

    /* Wait if we need to: */
    if (cur >= next)
        return;
    SDL_Delay(int(next - cur));
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

/* TODO: Move to classes. ControllerAssigner? */
static bool try_attach_gamepad(Tank * tank, int gamepad_num)
{
    if (SDL_NumJoysticks() < gamepad_num)
        return false;
    try
    {
        tank->SetController(std::make_shared<GamePadController>(gamepad_num));
        gamelib_print("Using Joystick #%d for player %d\n", gamepad_num, int(tank->GetColor()));
    }
    catch (const GameException & ex)
    {
        gamelib_print("Failed to use joystick #%d: %s\n", gamepad_num, ex.what());
        return false;
    }
    return true;
}

#define ONE_KEYBOARD SDL_SCANCODE_A, SDL_SCANCODE_D, SDL_SCANCODE_W, SDL_SCANCODE_S, SDL_SCANCODE_LCTRL
#define TWO_KEYBOARD_A SDL_SCANCODE_A, SDL_SCANCODE_D, SDL_SCANCODE_W, SDL_SCANCODE_S, SDL_SCANCODE_LCTRL
#define TWO_KEYBOARD_B SDL_SCANCODE_LEFT, SDL_SCANCODE_RIGHT, SDL_SCANCODE_UP, SDL_SCANCODE_DOWN, SDL_SCANCODE_SLASH

/* TODO: move to ControllerAssigner */
void gamelib_tank_attach(Tank * tank, int tank_num, int num_players)
{
    static bool used_controller = false; /* TODO: put into class state */
    if (num_players == 1 && tank_num == 0)
    {
        if (!try_attach_gamepad(tank, tank_num))
            tank->SetController(std::make_shared<KeyboardWithMouseController>(ONE_KEYBOARD));
    }
    else if (num_players == 2)
    {
        if (tank_num == 0)
        {
            if (!try_attach_gamepad(tank, tank_num)) /* Controller #0 */
                tank->SetController(std::make_shared<KeyboardWithMouseController>(ONE_KEYBOARD));
            else
                used_controller = true;
        }
        else if (tank_num == 1)
        {
            if (!try_attach_gamepad(tank, tank_num)) /* Controller #1 */
            {
                if (!used_controller)
                    throw NoControllersException("At least one controller needs to be attached for two-player mode");
                tank->SetController(std::make_shared<KeyboardWithMouseController>(ONE_KEYBOARD));
            }
        }
    }
}
