#pragma once
#include <SDL.h>
#include <tank.h>

void controller_keyboard_attach( Tank *, SDLKey, SDLKey, SDLKey, SDLKey, SDLKey);
void controller_joystick_attach( Tank *t ) ;
