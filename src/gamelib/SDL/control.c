#include <cstdio>
#include <ctime>
#include <SDL.h>

#include <gamelib.h>
#include <tank.h>
#include <tweak.h>

#include "control.h"
#include "controllersdl.h"
#include "require_sdl.h"


/* Set up SDL: */
int gamelib_init() {
	char text[1024];
	
	if( SDL_Init(SDL_INIT_EVERYTHING)<0 ) {
		gamelib_error("Failed to initialize SDL: %s\n", SDL_GetError());
		return 1;
	}
	
	/* Dump out the current graphics driver, just for kicks: */
	SDL_VideoDriverName( text, sizeof(text) );
	gamelib_print("Using video driver: %s\n", text);
	
	return 0;
}

/* Frees stuff up: */
int gamelib_exit() {
	SDL_Quit();
	return 0;
}

/* Waits long enough to maintain a consistent FPS: */
void smart_wait() {
	int cur, next;
	
	/* Get the current time, and the next time: */
	cur  = SDL_GetTicks();
	next = int((cur/tweak::perf::advance_step.count() + 1) * tweak::perf::advance_step.count());
	
	/* Wait if we need to: */
	if(cur >= next) return;
	SDL_Delay(next - cur);
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
int gamelib_get_max_players()    { return 2; }
bool gamelib_get_can_resize()     { return 1; }
bool gamelib_get_can_fullscreen() { return 1; }
bool gamelib_get_can_window()     { return 1; }
int gamelib_get_target_fps()     { return tweak::perf::target_fps; }



/* This is kind-of a stopgap, until I make a better controller... */
static int try_attach_joystick(Tank *t) {
	if(SDL_NumJoysticks() == 0) return 0;
	t->SetController(std::make_shared<JoystickController>());
	return 1;
}

#define ONE_KEYBOARD     SDLK_LEFT, SDLK_RIGHT, SDLK_UP, SDLK_DOWN, SDLK_LCTRL
#define TWO_KEYBOARD_A   SDLK_a, SDLK_d, SDLK_w, SDLK_s, SDLK_LCTRL
#define TWO_KEYBOARD_B   SDLK_LEFT, SDLK_RIGHT, SDLK_UP, SDLK_DOWN, SDLK_SLASH

/* TODO: *REALLY* de-uglyify this: */
int gamelib_tank_attach(Tank *t, int tank_num, int num_players) {
	if(num_players == 1 && tank_num == 0) {
		if(!try_attach_joystick(t))
			t->SetController(std::make_shared<KeyboardController>(ONE_KEYBOARD));
	
	} else if(num_players == 2) {
		if (tank_num == 0) {
			if(!try_attach_joystick(t))
				t->SetController(std::make_shared<KeyboardController>(TWO_KEYBOARD_A));
		
		} else if(tank_num == 1) {
			if(SDL_NumJoysticks())
				t->SetController(std::make_shared<KeyboardController>(ONE_KEYBOARD));
			else
				t->SetController(std::make_shared<KeyboardController>(TWO_KEYBOARD_B));
		
		} else return 1;
	}
	
	else return 1;
	
	return 0;
}

