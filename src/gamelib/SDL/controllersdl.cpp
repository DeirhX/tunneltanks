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
    output.is_crosshair_absolute = true;
    output.crosshair_screen_pos = {x, y};
    output.is_shooting = buttons & SDL_BUTTON(1);
    return output;
}

/*----------------------------------------------------------------------------*
 *   JOYSTICK                                                                 *
 *----------------------------------------------------------------------------*/

/* This is the joystick value (between 1 and 32767) where a joystick axis gets
 * interpretted as going in that direction: */

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

GamePadController::~GamePadController() { SDL_JoystickClose(this->joystick); }

ControllerOutput GamePadController::ApplyControls(PublicTankInfo * tankPublic)
{
    /* Where is this joystick pointing? Corresponds to left analog stick. Value range is -32K to +32K */
    Sint32 lx = SDL_JoystickGetAxis(this->joystick, 0);
    Sint32 ly = SDL_JoystickGetAxis(this->joystick, 1);

    auto output = ControllerOutput{};
    Uint32 dist = lx * lx + ly * ly;
    /* Don't do jack if the joystick is too close to its origin: */
    if (dist >= tweak::control::GamePadMovementThreshold * tweak::control::GamePadMovementThreshold)
    {
        int tx = (lx == 0) ? 0 : (abs(ly * 1000 / lx) < 2000);
        int ty = (lx == 0) ? 1 : (abs(ly * 1000 / lx) > 500);

        output.speed = {tx * (lx > 0 ? 1 : -1), ty * (ly > 0 ? 1 : -1)};
    }

    /* Get right analog stick */
    Sint32 rx = SDL_JoystickGetAxis(this->joystick, 4);
    Sint32 ry = SDL_JoystickGetAxis(this->joystick, 3);

    /* Apply aim threshold - even in neutral state the analog input is never truly 0 */
    if (rx > 0)
        rx = std::max(0, rx - tweak::control::GamePadAimThreshold);
    else
        rx = std::min(0, rx + tweak::control::GamePadAimThreshold);
    if (ry > 0)
        ry = std::max(0, ry - tweak::control::GamePadAimThreshold);
    else
        ry = std::min(0, ry + tweak::control::GamePadAimThreshold);

    // gamelib_print("Right stick: %d, %d           \r", rx, ry);

    /* Finally apply to crosshair */
    VectorF aim_dir = VectorF{float(rx), float(ry)} / std::numeric_limits<short>::max();
    if (aim_dir != VectorF{})
        output.crosshair_direction = DirectionF{aim_dir.Normalize()};
    else
        output.crosshair_direction = {};
    output.is_crosshair_absolute = false;

    /* Can't use lower buttons in SDL1. FU. */
    output.is_shooting = SDL_JoystickGetButton(this->joystick, 5) || SDL_JoystickGetButton(this->joystick, 4);
  
    return output;
}
