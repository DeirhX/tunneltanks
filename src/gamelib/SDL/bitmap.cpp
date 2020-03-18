#include "bitmap.h"
#include <algorithm>
#include <cassert>
#include <SDL.h>

#include <gamelib.h>
#include <types.h>
#include <memalloc.h>
#include <vector>


#include "exceptions.h"
#include "require_sdl.h"

void BmpFile::SaveToFile(const ColorBitmap& data, std::string_view file_name)
{
	auto native_surface = holder_with_deleter<SDL_Surface>(
		SDL_CreateRGBSurface(SDL_SWSURFACE, data.size.x, data.size.y, 24, 0, 0, 0, 0),
		[](SDL_Surface* surface) {SDL_FreeSurface(surface); });
		
	if (!native_surface)
		throw GameException("Failed to allocate surface");
	/* Raising this semaphore so often is probably gonna hurt performance, but
	 * it's not like we're generating a bmp every second...  :) */
	if (SDL_MUSTLOCK(native_surface)) SDL_LockSurface(native_surface.get());

	/* Get the address, and the mapped color: */
	assert(sizeof(Color) == native_surface->format->BytesPerPixel);
	assert(native_surface->pitch * native_surface->h == data.GetLength() * sizeof(Color));

	for (int i = 0; i < data.GetLength(); ++i)
	{
		std::uint32_t mapped_color = SDL_MapRGB(native_surface->format, data[i].r, data[i].g, data[i].b);
		std::memcpy(((std::uint8_t*)native_surface->pixels) + sizeof(Color) * i, &mapped_color, sizeof(Color));
	}

	//p = &((Uint8*)f->s->pixels)[y*f->s->pitch + x*f->s->format->BytesPerPixel];
	//
	/* ... and unlock, and we're good! :) */
	if (SDL_MUSTLOCK(native_surface)) SDL_UnlockSurface(native_surface.get());

	if (SDL_SaveBMP(native_surface.get(), file_name.data()) != 0)
		throw GameException("Failed save bitmap");
}

ColorBitmap BmpFile::LoadFromFile(std::string_view file_name)
{
	auto native_surface = std::unique_ptr<SDL_Surface, void(*)(SDL_Surface*)>(
		SDL_LoadBMP(file_name.data()),
		[](SDL_Surface* surface) {SDL_FreeSurface(surface); });

	if (!native_surface)
		throw GameException("Failed to load bitmap");

	assert(sizeof(Color) == native_surface->format->BytesPerPixel);
	auto loaded_data = ColorBitmap( Size{ native_surface->w, native_surface->h });

	for (int i = 0; i < native_surface->w * native_surface->h; ++i)
	{
		std::uint32_t mapped_color;
		std::memcpy(&mapped_color, &((Color*)native_surface->pixels)[i], sizeof(Color));
		SDL_GetRGB(mapped_color, native_surface->format, &loaded_data[i].r, &loaded_data[i].g, &loaded_data[i].b); /* It's super effective! ^_^ */
	}

	/* freed automatically */
	return loaded_data;
}
