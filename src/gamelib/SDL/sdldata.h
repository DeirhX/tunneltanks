#pragma once
#include <sdl2/include/SDL.h>

#define SDL_OPTIONS    (SDL_HWSURFACE | SDL_DOUBLEBUF | SDL_RESIZABLE)
#define SDL_OPTIONS_FS (SDL_HWSURFACE | SDL_DOUBLEBUF | SDL_FULLSCREEN)

typedef struct SDLData {
    SDL_Window * window;
    SDL_Surface * s;
    SDL_Texture * texture;
    SDL_Renderer * renderer;
    bool is_fullscreen;
} SDLData;

extern SDLData _DATA;
