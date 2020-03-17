#include <algorithm>
#include <cassert>
#include <SDL.h>

#include <gamelib.h>
#include <types.h>
#include <memalloc.h>
#include <vector>


#include "require_sdl.h"


struct BMPFile {
	SDL_Surface *s;
} ;


BMPFile *gamelib_bmp_new(int width, int height) {
	BMPFile *out;
	
	out = get_object(BMPFile);
	out->s = SDL_CreateRGBSurface(SDL_SWSURFACE, width, height, 24, 0, 0, 0, 0);
	
	return out;
}

void gamelib_bmp_set_data(BMPFile *f, const std::vector<Color>& data) {
	/* Raising this semaphore so often is probably gonna hurt performance, but
	 * it's not like we're generating a bmp every second...  :) */
	if(SDL_MUSTLOCK(f->s)) SDL_LockSurface(f->s);
	
	/* Get the address, and the mapped color: */
	assert(sizeof(Color) == f->s->format->BytesPerPixel);
	assert(f->s->pitch * f->s->h == data.size() * sizeof(Color));

	for(int i=0; i<data.size(); ++i)
	{
		std::uint32_t mapped_color = SDL_MapRGB(f->s->format, data[i].r, data[i].g, data[i].b);
		std::memcpy(((std::uint8_t*)f->s->pixels) + sizeof(Color) * i, &mapped_color, sizeof(Color));
	}
	
	//p = &((Uint8*)f->s->pixels)[y*f->s->pitch + x*f->s->format->BytesPerPixel];
	//mapped_color = 
	//
	/* ... and unlock, and we're good! :) */
	if(SDL_MUSTLOCK(f->s)) SDL_UnlockSurface(f->s);	
}

void gamelib_bmp_finalize(BMPFile *f, const char *filename) {
	/* It's just save... */
	SDL_SaveBMP(f->s, filename);
	
	/* ... and free: */
	SDL_FreeSurface(f->s);
	free_mem(f);
}

