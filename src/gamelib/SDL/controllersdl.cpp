#include <cstdlib>
#include <SDL.h>

#include <gamelib.h>
#include <tank.h>
#include <controllersdl.h>

#include "require_sdl.h"
#include "exceptions.h"

/* Any SDL-based controllers go in this file. */

/*----------------------------------------------------------------------------*
 *   KEYBOARD                                                                 *
 *----------------------------------------------------------------------------*/

KeyboardController::KeyboardController(SDLKey left, SDLKey right, SDLKey up, SDLKey down, SDLKey shoot)
	: left(left), right(right), up(up), down(down), shoot(shoot)
{
}

ControllerOutput KeyboardController::ApplyControls(PublicTankInfo *tankPublic) {
	Uint8 *keys = SDL_GetKeyState( NULL );
	
	return ControllerOutput{
		.speed {keys[this->right] - keys[this->left],
				keys[this->down] - keys[this->up]},
		.is_shooting = keys[this->shoot] != 0
	};
}


ControllerOutput KeyboardWithMouseController::ApplyControls(PublicTankInfo* tankPublic)
{
	auto output = Base::ApplyControls(tankPublic);
	int x, y;
	auto buttons = SDL_GetMouseState(&x, &y);
	output.crosshair = { x, y };
	output.is_shooting = buttons & SDL_BUTTON(1);
//	output.turret_dir = tankPublic->level_view.Height
	return output;
}


/*----------------------------------------------------------------------------*
 *   JOYSTICK                                                                 *
 *----------------------------------------------------------------------------*/

/* TODO: We are currently just letting the joystick be closed indirectly by the
 *       program exit. Fix this be making it possible for a controller to define
 *       a tear-down function. */


/* This is the joystick value (between 1 and 32767) where a joystick axis gets
 * interpretted as going in that direction: */
#define CUTOFF (10000)

JoystickController::JoystickController() 
{

	/* Make sure that this is even a joystick to connect to: */
	if (SDL_NumJoysticks() == 0) {
		/* TODO: exiting isn't all that friendly... we need a better controller API... */
		gamelib_debug("No joysticks connected.\n");
		exit(1);
	}

	this->joystick =  SDL_JoystickOpen(0);

	if (this->joystick) {
		gamelib_debug("Using Joystick #0:\n");
		gamelib_debug("  Name:    %s\n", SDL_JoystickName(0));
		gamelib_debug("  Axes:    %d\n", SDL_JoystickNumAxes(this->joystick));
		gamelib_debug("  Buttons: %d\n", SDL_JoystickNumButtons(this->joystick));
		gamelib_debug("  Balls:   %d\n", SDL_JoystickNumBalls(this->joystick));

	}
	else {
		gamelib_debug("Failed to open Joystick #0.\n");
		throw GameException("Failed to open Joystick #0");
	}
}


ControllerOutput JoystickController::ApplyControls(PublicTankInfo* tankPublic)
{
	Sint32 jx, jy;
	Uint32 dist;
	
	/* Where is this joystick pointing? */
	jx = SDL_JoystickGetAxis(this->joystick, 0);
	jy = SDL_JoystickGetAxis(this->joystick, 1);

	auto output = ControllerOutput{};
	dist = jx*jx + jy*jy;
	/* Don't do jack if the joystick is too close to its origin: */
	if(dist >= CUTOFF * CUTOFF) {
		int tx, ty;
		
		tx = (jx==0) ? 0 : ( abs(jy * 1000 / jx) < 2000 );
		ty = (jx==0) ? 1 : ( abs(jy * 1000 / jx) > 500 );
		
		output.speed = { tx * (jx > 0 ? 1 : -1),
						ty* (jy > 0 ? 1 : -1) };
	}
	output.is_shooting = SDL_JoystickGetButton(this->joystick, 0);
	return output;
}
