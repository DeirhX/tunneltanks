#pragma once
#include <SDL.h>
#include <controller.h>

class KeyboardController : public Controller
{
	SDL_Scancode left, right, up, down, shoot;
public:
    KeyboardController(SDL_Scancode left, SDL_Scancode right, SDL_Scancode up, SDL_Scancode down, SDL_Scancode shoot);
	ControllerOutput ApplyControls(PublicTankInfo* tankPublic) override;

	bool IsPlayer() override { return true; }
};

class KeyboardWithMouseController : public KeyboardController
{
	using Base = KeyboardController;
public:
    KeyboardWithMouseController(SDL_Scancode left, SDL_Scancode right, SDL_Scancode up, SDL_Scancode down,
                                SDL_Scancode shoot)
        :
		KeyboardController(left, right, up, down, shoot) {};

	ControllerOutput ApplyControls(PublicTankInfo* tankPublic) override;
};


/* The SDL-based keyboard controller: */
struct GamePadMapping
{
    int MoveHorizontalAxis;
    int MoveVerticalAxis;
    int AimHorizontalAxis;
    int AimVerticalAxis;
    int ShootPrimary;
    int ShootSecondary;
    int CyclePrimaryWeaponNext;
    int CyclePrimaryWeaponsPrev;
    int CycleSecondaryWeaponNext;
    int CycleSecondaryWeaponsPrev;
};

class GamePadController : public Controller
{
	SDL_Joystick* joystick;
    GamePadMapping mapping = {};

	bool was_cycle_primary_weapon_next_down = false;
    bool was_cycle_primary_weapon_prev_down = false;
    bool was_cycle_secondary_weapon_next_down = false;
    bool was_cycle_secondary_weapon_prev_down = false;
  public:
	GamePadController(int joy_index);
	~GamePadController();
	ControllerOutput ApplyControls(PublicTankInfo* tankPublic) override;

	bool IsPlayer() override { return true; }
};

