#pragma once
#include <SDL.h>
#include "sdldata.h"

void smart_wait();

/* The main loop function will call the draw func at regular intervals, and then
 * flip the draw buffer: */
template <typename AdvanceFunc>
void gamelib_main_loop(AdvanceFunc advance_func) {
	while (true) {

		if (!advance_func())
			return;

		smart_wait();
        SDL_UpdateTexture(_DATA.texture, nullptr, _DATA.s->pixels, _DATA.s->pitch);
        SDL_RenderClear(_DATA.renderer);
        SDL_RenderCopy(_DATA.renderer, _DATA.texture, nullptr, nullptr);
        SDL_RenderPresent(_DATA.renderer);
	}
}

