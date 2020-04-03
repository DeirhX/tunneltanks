#pragma once
/* This file defines an interface that is implemented by one of this folder's
 * subdirectories. Most functions in here are normally provided by SDL, but are
 * abstracted so that this game can be used in non-SDL environments. (Namely:
 * Android.) */

#include <types.h>
#include <vector>

#include "game_config.h"

/* If the gamelib needs initialization, this'll do it: */
void gamelib_init();
/* If the gamelib needs to free resources before exiting, this'll do it: */
void gamelib_exit();

/* Gives a way to poll the gamelib for the capabilities provided by the
 * underlying system: */
int gamelib_get_max_players();     /* Returns 1 or 2. */
bool gamelib_get_can_resize();     /* Returns 0 or 1. */
bool gamelib_get_can_fullscreen(); /* Returns 0 or 1. */
bool gamelib_get_can_window();     /* Returns 0 or 1. */
int gamelib_get_target_fps();      /* Usually returns 24. */

/* Some platforms (Android) will be acting as the game loop, so the game loop
 * needs to happen in the gamelib: */
typedef int (*draw_func)(void * data);

/* This lets you attach controllers to a tank: */
void gamelib_tank_attach(class Tank * tank, int tank_num, int num_players);

/* TODO: This will need a means for configuring the controller... */

/* Allow us to handle events in a fairly platform-neutral way: */
enum class GameEvent
{
    None = 0,
    Exit,
    Resize,
    ToggleFullscreen,
};

GameEvent gamelib_event_get_type();
Rect gamelib_event_resize_get_size(); /* Returns {0,0,0,0} on fail. */
void gamelib_event_done();

/*
 * RenderedPixel: Possibly an exact memory layout of a pixel that's going to be directly copied into video memory.
 *   If matched exactly, no conversion will be needed and we can copy entire array from RAM into VRAM.
 */
struct RenderedPixel
{
    uint8_t r;
    uint8_t g;
    uint8_t b;
    uint8_t a;
};

/*
 * RenderSurface: An array of raw (pixel) color information that can
 *  be effectively rendered into device video memory through a Renderer
 */
class RenderSurface
{
    std::vector<RenderedPixel> surface;
  public:
    RenderSurface(Size size) : surface(size.x * size.y) {}
};

/*
 * Renderer: Takes care of rendering our RenderSurface to an actual device.
 */
class Renderer
{
  public:
    virtual ~Renderer() = default;
    virtual void DrawPixel(NativeScreenPosition position, Color32 color) = 0;
    virtual void DrawRectangle(NativeRect rect, Color32 color) = 0;
    virtual void SetSurfaceResolution(Size size) = 0;
    virtual Size GetSurfaceResolution() = 0;
    virtual void RenderFrame() = 0;
};

/*
 * Window: Represents the game native window
 */
class Window
{
  public:
    virtual ~Window() = default;
    virtual bool IsFullscreen() = 0;
    virtual Size GetResolution() = 0;
    virtual void Resize(Size size, bool is_fullscreen) = 0;
    //virtual Renderer * GetRenderer() = 0;
};

/*
 * Cursor: Control the visibility and possibly shape of mouse cursor.
 */
class Cursor
{
  public:
    virtual ~Cursor() = default;
    virtual void Hide() = 0;
    virtual void Show() = 0;
};

/*
 *  System: Represents the input/output system of the game - window, renderer, cursor and others in the future.
 *  We can afford the virtual calls here - none of this is going to be called too often.
 */
class GameSystem
{
  public:
    virtual Renderer * GetRenderer() = 0;
    virtual Window * GetWindow() = 0;
    virtual Cursor * GetCursor() = 0;
};

inline std::unique_ptr<GameSystem> global_game_system;
std::unique_ptr<GameSystem> CreateGameSystem(VideoConfig video_config);
inline GameSystem * GetSystem() { return global_game_system.get(); }

/* A few outputting commands: */
void gamelib_print(const char * str, ...);
void gamelib_debug(const char * str, ...);
void gamelib_error(const char * str, ...);
