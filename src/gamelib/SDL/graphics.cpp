#include <SDL.h>

#include <gamelib.h>
#include <memory>
#include <types.h>

#include "require_sdl.h"
#include "sdldata.h"

//SdlScreen

/* Will select the best fullscreen resolution based on pixel count: */
static SDL_Rect screen_get_best_resolution()
{
    SDL_Rect ** modes;
    int i;
    SDL_Rect out = {0, 0, 0, 0};
    int out_score = 0;

    modes = SDL_ListModes(NULL, SDL_OPTIONS_FS);
    if (!modes)
        return out;

    /* Are all resolutions available? */
    if (modes == (SDL_Rect **)-1)
    {
        /*out.w = SCREEN_WIDTH; out.h = SCREEN_HEIGHT;*/
        return out;
    }

    for (i = 0; modes[i]; i++)
    {
        if (modes[i]->w * modes[i]->h > out_score)
        {
            out = *modes[i];
            out_score = out.w * out.h;
        }
    }

    return out;
}

/* Sets the display to fullscreen, calculating the best resolution: */
int gamelib_set_fullscreen()
{
    SDL_Surface * newsurface;
    SDL_Rect r = screen_get_best_resolution();

    /* Already fullscreen? */
    if (_DATA.is_fullscreen)
        return 0;

    /* Actually set the new video mode: */
    if (!(newsurface = SDL_SetVideoMode(r.w, r.h, 0, SDL_OPTIONS_FS)))
    {
        gamelib_error("Failed to set video mode: %s\n", SDL_GetError());
        return 1;
    }

    /* Disable the mouse cursor in fullscreen mode: */
    SDL_ShowCursor(SDL_DISABLE);

    /* Update all of the internal variables: */
    _DATA.s = newsurface;
    _DATA.is_fullscreen = !!(_DATA.s->flags & SDL_FULLSCREEN);

    return 0;
}

/* Sets the display to windowed mode, with given dimensions: */
int gamelib_set_window(Size size)
{
    SDL_Surface * newsurface;

    /* Actually set the new video mode: */
    if (!(newsurface = SDL_SetVideoMode(size.x, size.y, 0, SDL_OPTIONS)))
    {
        gamelib_error("Failed to set video mode: %s\n", SDL_GetError());
        return 1;
    }

    /* Enable the mouse cursor in windowed mode: */
    SDL_ShowCursor(SDL_ENABLE);

    /* Update all of the internal variables: */
    _DATA.s = newsurface;
    _DATA.is_fullscreen = !!(_DATA.s->flags & SDL_FULLSCREEN);

    return 0;
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
