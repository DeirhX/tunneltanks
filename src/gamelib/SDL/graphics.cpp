#include <SDL.h>

#include <gamelib.h>
#include <memory>
#include <types.h>

#include "exceptions.h"
#include "require_sdl.h"
#include "sdldata.h"
#include "tweak.h"
//SdlScreen

void initialize_renderer(SDL_Window * window, Size render_size)
{
    SDL_Renderer * renderer = SDL_CreateRenderer(window, -1, SDL_RENDERER_ACCELERATED | SDL_RENDERER_PRESENTVSYNC);
    if (!renderer)
        throw GameInitException("Failed to create game renderer.");
    SDL_RenderSetLogicalSize(renderer, render_size.x, render_size.y);

    SDL_Texture * texture = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_ARGB8888, SDL_TEXTUREACCESS_STREAMING,
                                                 render_size.x, render_size.y);
    SDL_Surface * surface = SDL_CreateRGBSurface(0, render_size.x, render_size.y, 32, 0, 0, 0, 0);

    /* Enable the mouse cursor in windowed mode: */
    SDL_ShowCursor(SDL_ENABLE);

    /* Update all of the internal variables: */
    _DATA.s = surface;
    _DATA.texture = texture;
    _DATA.renderer = renderer;
    _DATA.is_fullscreen = !!(_DATA.s->flags & SDL_WINDOW_FULLSCREEN_DESKTOP);
}
/* Sets the display to fullscreen, calculating the best resolution: */
void gamelib_set_fullscreen(Size size)
{
    SDL_DestroyWindow(_DATA.window);

    SDL_Window * sdlWindow =
        SDL_CreateWindow(tweak::system::WindowTitle, SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, 0, 0,
                         SDL_WINDOW_FULLSCREEN_DESKTOP);

    if (!sdlWindow)
        throw GameInitException("Failed to create game window.");

    _DATA.window = sdlWindow;
    initialize_renderer(sdlWindow, tweak::screen::size);
}

/* Sets the display to windowed mode, with given dimensions: */
void gamelib_set_window(Size size)
{
    SDL_DestroyWindow(_DATA.window);

    SDL_Window * sdlWindow =
        SDL_CreateWindow(tweak::system::WindowTitle, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, size.x, size.y,
                         SDL_WINDOW_RESIZABLE | SDL_WINDOW_ALLOW_HIGHDPI);
    if (!sdlWindow)
        throw GameInitException("Failed to create game window.");

    _DATA.window = sdlWindow;
    initialize_renderer(sdlWindow, size);
}

/* Returns the screen dimensions in a Rect struct: */
Size gamelib_get_resolution()
{
    if (!_DATA.s)
        return {};
    return {static_cast<int>(_DATA.s->w), static_cast<int>(_DATA.s->h)};
}

/* Returns whether the current graphics mode is fullscreen: */
bool gamelib_get_fullscreen() { return _DATA.is_fullscreen; }

/* Will set a given rectangle to a given color. (NULL rect is fullscreen): */
int gamelib_draw_box(NativeRect rect, Color32 color)
{
    Uint32 c = SDL_MapRGBA(_DATA.s->format, color.r, color.g, color.b, color.a);
    auto r = SDL_Rect{(Sint16)rect.pos.x, (Sint16)rect.pos.y, (Uint16)rect.size.x, (Uint16)rect.size.y};
    SDL_FillRect(_DATA.s, &r, c);
    return 0;
}

holder_with_deleter<SDL_Cursor> global_cursor;

void gamelib_enable_cursor() { SDL_ShowCursor(SDL_ENABLE); }
void gamelib_disable_cursor()
{
    int32_t cursorData[2] = {0, 0};
    global_cursor = holder_with_deleter<SDL_Cursor>(
        SDL_CreateCursor((Uint8 *)cursorData, (Uint8 *)cursorData, 8, 8, 4, 4),
        [](SDL_Cursor * cursor) { /*SDL_FreeCursor(cursor);*/
        }); /* No need for now because active one is freed automatically. TODO: Needed when we use more than one. */

    SDL_SetCursor(global_cursor.get());
    SDL_ShowCursor(SDL_DISABLE);
}
/* The display is double-buffered, so double buffer it: *
int gamelib_flip() {
	SDL_Flip(_DATA.s);
	return 0;
}
*/
