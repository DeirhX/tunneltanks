#pragma once
#include "controller.h"
#include <sdl2/include/SDL.h>

class KeyboardController : public Controller
{
    SDL_Scancode left, right, up, down, shoot;

  public:
    KeyboardController(SDL_Scancode left, SDL_Scancode right, SDL_Scancode up, SDL_Scancode down, SDL_Scancode shoot);
    ControllerOutput ApplyControls(PublicTankInfo * tankPublic) override;

    bool IsPlayer() override { return true; }
};

class KeyboardWithMouseController : public KeyboardController
{
    using Base = KeyboardController;

  public:
    KeyboardWithMouseController(SDL_Scancode left, SDL_Scancode right, SDL_Scancode up, SDL_Scancode down,
                                SDL_Scancode shoot)
        : KeyboardController(left, right, up, down, shoot){};

    ControllerOutput ApplyControls(PublicTankInfo * tankPublic) override;
};

/* The SDL-based keyboard controller: */
struct GamePadMapping
{
    enum class MappingType
    {
        Invalid,
        Button,
        Axis,
        Pad,
    };
    struct ButtonOrAxis
    {
        int8_t ordinal = -1;
        MappingType type = MappingType::Invalid;
        int16_t axis_threshold = 0;

        constexpr ButtonOrAxis() = default;
        constexpr ButtonOrAxis(int8_t ordinal, MappingType type, int16_t threshold = 0) : ordinal(ordinal), type(type), axis_threshold(threshold) { }

        constexpr int8_t Id() const { return ordinal; }
        constexpr MappingType Type() const { return type; }
        int16_t CurrentAxisValue(SDL_Joystick * joystick) const
        {
            assert(this->type == MappingType::Axis);
            return SDL_JoystickGetAxis(joystick, this->ordinal);
        }
        bool IsPressed(SDL_Joystick * joystick) const
        {
            if (this->type == MappingType::Button)
                return SDL_JoystickGetButton(joystick, this->ordinal);
            else if (this->type == MappingType::Axis)
                return axis_threshold > 0 ? this->CurrentAxisValue(joystick) > axis_threshold
                                          : this->CurrentAxisValue(joystick) > axis_threshold;
            else if (this->type == MappingType::Pad)
                return SDL_JoystickGetHat(joystick, 0) == this->ordinal;
            else
            {
                assert(!"Uninitialized control");
                return false;
            }
        }
    };
    struct Button : public ButtonOrAxis
    {
        constexpr Button() : ButtonOrAxis(-1, MappingType::Button) {}
        constexpr Button(int8_t ordinal) : ButtonOrAxis(ordinal, MappingType::Button) {}
    };
    struct Axis : public ButtonOrAxis
    {
        constexpr Axis() : ButtonOrAxis(-1, MappingType::Axis) {}
        constexpr Axis(int8_t ordinal, int16_t threshold = 1000) : ButtonOrAxis(ordinal, MappingType::Axis, threshold) {}
    };
    struct Pad : public ButtonOrAxis
    {
        constexpr Pad() : ButtonOrAxis(-1, MappingType::Pad) {}
        constexpr Pad(int8_t ordinal) : ButtonOrAxis(ordinal, MappingType::Pad) {}
    };

    Axis MoveHorizontalAxis;
    Axis MoveVerticalAxis;
    Axis AimHorizontalAxis;
    Axis AimVerticalAxis;
    ButtonOrAxis ShootPrimary;
    ButtonOrAxis ShootSecondary;
    ButtonOrAxis ShootTertiary;
    ButtonOrAxis CyclePrimaryWeaponNext;
    ButtonOrAxis CyclePrimaryWeaponsPrev;
    ButtonOrAxis CycleSecondaryWeaponNext;
    ButtonOrAxis CycleSecondaryWeaponsPrev;
    Button BuildPrimary;
    Button BuildSecondary;
    Button BuildTertiary;
};

class GamePadController : public Controller
{
    SDL_Joystick * joystick;
    GamePadMapping mapping;

    bool was_cycle_primary_weapon_next_down = false;
    bool was_cycle_primary_weapon_prev_down = false;
    bool was_cycle_secondary_weapon_next_down = false;
    bool was_cycle_secondary_weapon_prev_down = false;

    bool was_build_primary_down = false;
    bool was_build_secondary_down = false;
    bool was_build_tertiary_down = false;

  public:
    GamePadController(int joy_index);
    ~GamePadController();
    ControllerOutput ApplyControls(PublicTankInfo * tankPublic) override;

    bool IsPlayer() override { return true; }
};
