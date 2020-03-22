#include <SDL.h>
#include <cstdlib>

#include <controllersdl.h>
#include <gamelib.h>
#include <tank.h>

#include "exceptions.h"
#include "require_sdl.h"

/* Any SDL-based controllers go in this file. */

/*----------------------------------------------------------------------------*
 *   KEYBOARD                                                                 *
 *----------------------------------------------------------------------------*/

KeyboardController::KeyboardController(SDLKey left, SDLKey right, SDLKey up, SDLKey down, SDLKey shoot)
    : left(left), right(right), up(up), down(down), shoot(shoot)
{
    gamelib_print("Using Keyboard #0:\n");
}

ControllerOutput KeyboardController::ApplyControls(PublicTankInfo * tankPublic)
{
    Uint8 * keys = SDL_GetKeyState(NULL);

    return ControllerOutput{.speed{keys[this->right] - keys[this->left], keys[this->down] - keys[this->up]},
                            .is_shooting = keys[this->shoot] != 0};
}

ControllerOutput KeyboardWithMouseController::ApplyControls(PublicTankInfo * tankPublic)
{
    auto output = Base::ApplyControls(tankPublic);
    int x, y;
    auto buttons = SDL_GetMouseState(&x, &y);
    output.crosshair = {x, y};
    output.is_shooting = buttons & SDL_BUTTON(1);
    return output;
}

/*----------------------------------------------------------------------------*
 *   JOYSTICK                                                                 *
 *----------------------------------------------------------------------------*/

/* This is the joystick value (between 1 and 32767) where a joystick axis gets
 * interpretted as going in that direction: */
constexpr int GamePadMoveTankCutoff = 10000;

GamePadController::GamePadController()
{
    /* Make sure that this is even a joystick to connect to: */
    if (SDL_NumJoysticks() == 0)
    {
        throw GameException("No joysticks connected.\n");
    }

    this->joystick = SDL_JoystickOpen(0);

    if (this->joystick)
    {
        gamelib_print("Using Joystick #0:\n");
        gamelib_print("  Name:    %s\n", SDL_JoystickName(0));
        gamelib_print("  Axes:    %d\n", SDL_JoystickNumAxes(this->joystick));
        gamelib_print("  Buttons: %d\n", SDL_JoystickNumButtons(this->joystick));
        gamelib_print("  Balls:   %d\n", SDL_JoystickNumBalls(this->joystick));
    }
    else
    {
        throw GameException("Failed to open Joystick #0");
    }
}

GamePadController::~GamePadController()
{
    SDL_JoystickClose(this->joystick);
}

ControllerOutput GamePadController::ApplyControls(PublicTankInfo * tankPublic)
{
    /* Where is this joystick pointing? */
    Sint32 jx = SDL_JoystickGetAxis(this->joystick, 0);
    Sint32 jy = SDL_JoystickGetAxis(this->joystick, 1);

    auto output = ControllerOutput{};
    Uint32 dist = jx * jx + jy * jy;
    /* Don't do jack if the joystick is too close to its origin: */
    if (dist >= GamePadMoveTankCutoff * GamePadMoveTankCutoff)
    {
        int tx = (jx == 0) ? 0 : (abs(jy * 1000 / jx) < 2000);
        int ty = (jx == 0) ? 1 : (abs(jy * 1000 / jx) > 500);

        output.speed = {tx * (jx > 0 ? 1 : -1), ty * (jy > 0 ? 1 : -1)};
    }
    output.is_shooting = SDL_JoystickGetButton(this->joystick, 0);

    return output;
}
