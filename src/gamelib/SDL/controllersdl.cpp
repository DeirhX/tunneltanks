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

KeyboardController::KeyboardController(SDL_Scancode left, SDL_Scancode right, SDL_Scancode up, SDL_Scancode down,
                                       SDL_Scancode shoot)
    : left(left), right(right), up(up), down(down), shoot(shoot)
{
    gamelib_print("Using Keyboard #0:\n");
}

ControllerOutput KeyboardController::ApplyControls(PublicTankInfo * tankPublic)
{
    int num_keys;
    const Uint8 * keys = SDL_GetKeyboardState(&num_keys);
    assert(num_keys >
           std::max(this->right, std::max(this->left, std::max(this->down, std::max(this->up, this->shoot)))));

    return ControllerOutput{.speed{keys[this->right] - keys[this->left], keys[this->down] - keys[this->up]},
                            .is_shooting_primary = keys[this->shoot] != 0};
}

ControllerOutput KeyboardWithMouseController::ApplyControls(PublicTankInfo * tankPublic)
{
    auto output = Base::ApplyControls(tankPublic);
    int x, y;
    auto buttons = SDL_GetMouseState(&x, &y);
    output.is_crosshair_absolute = true;
    output.crosshair_screen_pos = {x, y};
    output.is_shooting_primary = buttons & SDL_BUTTON(1);
    return output;
}

/*----------------------------------------------------------------------------*
 *   JOYSTICK                                                                 *
 *----------------------------------------------------------------------------*/

/* This is the joystick value (between 1 and 32767) where a joystick axis gets
 * interpretted as going in that direction: */

constexpr GamePadMapping XBox360Pad = {
    .MoveHorizontalAxis = 0,
    .MoveVerticalAxis   = 1,
    .AimHorizontalAxis  = 4,
    .AimVerticalAxis    = 3,
    .ShootPrimary       = 5,
    .ShootSecondary     = 4,
    .CyclePrimaryWeaponNext   = 2,
    .CyclePrimaryWeaponsPrev   = 0,
    .CycleSecondaryWeaponNext = 3,
    .CycleSecondaryWeaponsPrev = 1,

};

constexpr GamePadMapping PS4Pad = {
    .MoveHorizontalAxis = 0,
    .MoveVerticalAxis =   1,
    .AimHorizontalAxis =  2,
    .AimVerticalAxis =    3,
    .ShootPrimary =       5,
    .ShootSecondary =     4,
    .CyclePrimaryWeaponNext   = 0,
    .CyclePrimaryWeaponsPrev   = 1,
    .CycleSecondaryWeaponNext = 2,
    .CycleSecondaryWeaponsPrev = 3,
};

GamePadController::GamePadController(int joy_index)
{
    /* Make sure that this is even a joystick to connect to: */
    if (SDL_NumJoysticks() < joy_index)
    {
        throw NoControllersException("Not enough joysticks connected.\n");
    }

    this->joystick = SDL_JoystickOpen(joy_index);

    if (this->joystick)
    {
        gamelib_print("Using Joystick #:\n", joy_index);
        gamelib_print("  Name:    %s\n", SDL_JoystickName(this->joystick));
        gamelib_print("  Axes:    %d\n", SDL_JoystickNumAxes(this->joystick));
        gamelib_print("  Buttons: %d\n", SDL_JoystickNumButtons(this->joystick));
        gamelib_print("  Balls:   %d\n", SDL_JoystickNumBalls(this->joystick));

        if (!strcmp(SDL_JoystickName(this->joystick), "Wireless Controller"))
            this->mapping = PS4Pad;
        else
            this->mapping = XBox360Pad;
    }
    else
    {
        throw NoControllersException("Failed to open Joystick");
    }
}

GamePadController::~GamePadController() { SDL_JoystickClose(this->joystick); }

ControllerOutput GamePadController::ApplyControls(PublicTankInfo * tankPublic)
{
    /* Where is this joystick pointing? Corresponds to left analog stick. Value range is -32K to +32K */
    Sint32 lx = SDL_JoystickGetAxis(this->joystick, this->mapping.MoveHorizontalAxis);
    Sint32 ly = SDL_JoystickGetAxis(this->joystick, this->mapping.MoveVerticalAxis);

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
    Sint32 rx = SDL_JoystickGetAxis(this->joystick, this->mapping.AimHorizontalAxis);
    Sint32 ry = SDL_JoystickGetAxis(this->joystick, this->mapping.AimVerticalAxis);

    /* Apply aim threshold - even in neutral state the analog input is never truly 0 */
    if (rx > 0)
        rx = std::max(0, rx - tweak::control::GamePadAimThreshold);
    else
        rx = std::min(0, rx + tweak::control::GamePadAimThreshold);
    if (ry > 0)
        ry = std::max(0, ry - tweak::control::GamePadAimThreshold);
    else
        ry = std::min(0, ry + tweak::control::GamePadAimThreshold);

    //gamelib_print("Right stick: %d, %d           \r", rx, ry);

    /* Finally apply to crosshair */
    VectorF aim_dir = VectorF{float(rx), float(ry)} / std::numeric_limits<short>::max();
    if (aim_dir != VectorF{})
        output.crosshair_direction = DirectionF{aim_dir.Normalize()};
    else
        output.crosshair_direction = {};
    output.is_crosshair_absolute = false;

    /* Can't use lower buttons in SDL1. FU. */
    output.is_shooting_primary = SDL_JoystickGetButton(this->joystick, this->mapping.ShootPrimary);
    output.is_shooting_secondary= SDL_JoystickGetButton(this->joystick, this->mapping.ShootSecondary);

    /* Cycle Primary & Secondary weapons */
    bool cycle_primary_weapon_next = !!SDL_JoystickGetButton(this->joystick, this->mapping.CyclePrimaryWeaponNext);
    output.switch_primary_weapon_next = cycle_primary_weapon_next && !this->was_cycle_primary_weapon_next_down;
    this->was_cycle_primary_weapon_next_down = cycle_primary_weapon_next;
    
    bool cycle_primary_weapon_prev = !!SDL_JoystickGetButton(this->joystick, this->mapping.CyclePrimaryWeaponsPrev);
    output.switch_primary_weapon_prev = cycle_primary_weapon_prev && !this->was_cycle_primary_weapon_prev_down;
    this->was_cycle_primary_weapon_prev_down = cycle_primary_weapon_prev;

    bool cycle_secondary_weapon_next = !!SDL_JoystickGetButton(this->joystick, this->mapping.CycleSecondaryWeaponNext);
    output.switch_secondary_weapon_next = cycle_secondary_weapon_next && !this->was_cycle_secondary_weapon_next_down;
    this->was_cycle_secondary_weapon_next_down = cycle_secondary_weapon_next;

    bool cycle_secondary_weapon_prev = !!SDL_JoystickGetButton(this->joystick, this->mapping.CycleSecondaryWeaponsPrev);
    output.switch_secondary_weapon_prev = cycle_secondary_weapon_prev && !this->was_cycle_secondary_weapon_prev_down;
    this->was_cycle_secondary_weapon_prev_down = cycle_secondary_weapon_prev;

    return output;
}
