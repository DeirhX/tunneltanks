#pragma once
#include <SDL.h>
#include <controller.h>

class KeyboardController : public Controller
{
	SDLKey left, right, up, down, shoot;
public:
	KeyboardController(SDLKey left, SDLKey right, SDLKey up, SDLKey down, SDLKey shoot);
	ControllerOutput ApplyControls(PublicTankInfo* tankPublic) override;

	bool IsPlayer() override { return true; }
};

class KeyboardWithMouseController : public KeyboardController
{
	using Base = KeyboardController;
public:
	KeyboardWithMouseController(SDLKey left, SDLKey right, SDLKey up, SDLKey down, SDLKey shoot) :
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
};

class GamePadController : public Controller
{
	SDL_Joystick* joystick;
    GamePadMapping mapping;

  public:
	GamePadController(int joy_index);
	~GamePadController();
	ControllerOutput ApplyControls(PublicTankInfo* tankPublic) override;

	bool IsPlayer() override { return true; }
};

