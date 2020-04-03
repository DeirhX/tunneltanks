#pragma once
#include <cstdint>
#include <vector>
#include "game_config.h"
#include "render_surface.h"
#include "types.h"


/*
 * Renderer: Takes care of rendering our RenderSurface to an actual device.
 */
class Renderer
{
  public:
    virtual ~Renderer() = default;

    virtual void SetSurfaceResolution(Size size) = 0;
    virtual Size GetSurfaceResolution() = 0;
    virtual void RenderFrame(const RenderSurface * surface) = 0;
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
  protected:
    RenderSurface render_surface;
  public:
    GameSystem(Size render_surface_size) : render_surface(render_surface_size) {}
    RenderSurface * GetSurface() { return &render_surface; } /* This should be effective, we'll be copying pixels left and right */

    virtual Renderer * GetRenderer() = 0;
    virtual Window * GetWindow() = 0;
    virtual Cursor * GetCursor() = 0;
};

inline std::unique_ptr<GameSystem> global_game_system;
std::unique_ptr<GameSystem> CreateGameSystem(VideoConfig video_config);
inline GameSystem * GetSystem() { return global_game_system.get(); }