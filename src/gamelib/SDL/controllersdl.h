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
class JoystickController : public Controller
{
	SDL_Joystick* joystick;
public:
	JoystickController();
	ControllerOutput ApplyControls(PublicTankInfo* tankPublic) override;

	bool IsPlayer() override { return true; }
};

