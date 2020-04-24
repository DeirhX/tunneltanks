#include "sdl_controller.h"
#include "exceptions.h"
#include "game_system.h"
#include "gamelib.h"
#include "require_sdl.h"
#include "tank.h"
#include <cstdlib>
#include <SDL.h>

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

ControllerOutput KeyboardController::ApplyControls(PublicTankInfo *)
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
    Size window_size = GetSystem()->GetWindow()->GetResolution();
    output.crosshair_screen_pos = {static_cast<float>(x) / float(window_size.x), static_cast<float>(y) / float(window_size.y)};
    output.is_shooting_primary = buttons & SDL_BUTTON(1);
    output.build_primary = buttons & SDL_BUTTON(2);
    return output;
}

/*----------------------------------------------------------------------------*
 *   JOYSTICK                                                                 *
 *----------------------------------------------------------------------------*/

/* This is the joystick value (between 1 and 32767) where a joystick axis gets
 * interpretted as going in that direction: */

constexpr GamePadMapping XBox360Pad = {
    .MoveHorizontalAxis = GamePadMapping::Axis{0},
    .MoveVerticalAxis = GamePadMapping::Axis{1},
    .AimHorizontalAxis = GamePadMapping::Axis{3},
    .AimVerticalAxis = GamePadMapping::Axis{4},
    .ShootPrimary = GamePadMapping::Button{5},
    .ShootSecondary = GamePadMapping::Button{4},
    .ShootTertiary = GamePadMapping::Axis{5},
    .CyclePrimaryWeaponNext = GamePadMapping::Pad{2},
    .CyclePrimaryWeaponsPrev = GamePadMapping::Pad{8},
    .CycleSecondaryWeaponNext = GamePadMapping::Pad{4},
    .CycleSecondaryWeaponsPrev = GamePadMapping::Pad{1},
    .BuildPrimary = GamePadMapping::Button{2},
    .BuildSecondary = GamePadMapping::Button{3},
    .BuildTertiary = GamePadMapping::Button{0},
};

constexpr GamePadMapping PS4Pad = {
    .MoveHorizontalAxis = GamePadMapping::Axis{0},
    .MoveVerticalAxis = GamePadMapping::Axis{1},
    .AimHorizontalAxis = GamePadMapping::Axis{2},
    .AimVerticalAxis = GamePadMapping::Axis{3},
    .ShootPrimary = GamePadMapping::Button{10},
    .ShootSecondary = GamePadMapping::Button{9},
    .ShootTertiary = GamePadMapping::Axis{5, -20000},
    .CyclePrimaryWeaponNext = GamePadMapping::Button{14},
    .CyclePrimaryWeaponsPrev = GamePadMapping::Button{13},
    .CycleSecondaryWeaponNext = GamePadMapping::Button{12},
    .CycleSecondaryWeaponsPrev = GamePadMapping::Button{11},
    .BuildPrimary = GamePadMapping::Button{0},
    .BuildSecondary = GamePadMapping::Button{2},
    .BuildTertiary = GamePadMapping::Button{1},
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

        if (!strcmp(SDL_JoystickName(this->joystick), "PS4 Controller"))
            this->mapping = PS4Pad;
        else if (!strcmp(SDL_JoystickName(this->joystick), "Xbox One Elite Controller"))
            this->mapping = XBox360Pad;
        else
        {
            gamelib_print("Unsupported controller detected, trying to default to Xbox One controller...");
            this->mapping = XBox360Pad;
        }
    }
    else
    {
        throw NoControllersException("Failed to open Joystick");
    }
}

GamePadController::~GamePadController() { SDL_JoystickClose(this->joystick); }

ControllerOutput GamePadController::ApplyControls(PublicTankInfo *)
{
    /* Where is this joystick pointing? Corresponds to left analog stick. Value range is -32K to +32K */
    int lx = this->mapping.MoveHorizontalAxis.CurrentAxisValue(this->joystick);
    int ly = this->mapping.MoveVerticalAxis.CurrentAxisValue(this->joystick);

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
    int rx = this->mapping.AimHorizontalAxis.CurrentAxisValue(this->joystick);
    int ry = this->mapping.AimVerticalAxis.CurrentAxisValue(this->joystick);

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

    output.is_shooting_primary = this->mapping.ShootPrimary.IsPressed(this->joystick);
    output.is_shooting_secondary = this->mapping.ShootSecondary.IsPressed(this->joystick);
    output.is_shooting_tertiary = this->mapping.ShootTertiary.IsPressed(this->joystick);

    /* Cycle Primary & Secondary weapons */
    bool cycle_primary_weapon_next = this->mapping.CyclePrimaryWeaponNext.IsPressed(this->joystick);
    output.switch_primary_weapon_next = cycle_primary_weapon_next && !this->was_cycle_primary_weapon_next_down;
    this->was_cycle_primary_weapon_next_down = cycle_primary_weapon_next;

    bool cycle_primary_weapon_prev = this->mapping.CyclePrimaryWeaponsPrev.IsPressed(this->joystick);
    output.switch_primary_weapon_prev = cycle_primary_weapon_prev && !this->was_cycle_primary_weapon_prev_down;
    this->was_cycle_primary_weapon_prev_down = cycle_primary_weapon_prev;

    bool cycle_secondary_weapon_next = this->mapping.CycleSecondaryWeaponNext.IsPressed(this->joystick);
    output.switch_secondary_weapon_next = cycle_secondary_weapon_next && !this->was_cycle_secondary_weapon_next_down;
    this->was_cycle_secondary_weapon_next_down = cycle_secondary_weapon_next;

    bool cycle_secondary_weapon_prev = this->mapping.CycleSecondaryWeaponsPrev.IsPressed(this->joystick);
    output.switch_secondary_weapon_prev = cycle_secondary_weapon_prev && !this->was_cycle_secondary_weapon_prev_down;
    this->was_cycle_secondary_weapon_prev_down = cycle_secondary_weapon_prev;

    bool build_primary = this->mapping.BuildPrimary.IsPressed(this->joystick);
    output.build_primary = build_primary && !this->was_build_primary_down;
    this->was_build_primary_down = build_primary;

    bool build_secondary = this->mapping.BuildSecondary.IsPressed(this->joystick);
    output.build_secondary = build_secondary && !this->was_build_secondary_down;
    this->was_build_secondary_down = build_secondary;

    bool build_tertiary = this->mapping.BuildTertiary.IsPressed(this->joystick);
    output.build_tertiary = build_tertiary && !this->was_build_tertiary_down;
    this->was_build_tertiary_down = build_tertiary;

    return output;
}
