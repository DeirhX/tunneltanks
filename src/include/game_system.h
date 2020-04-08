#pragma once
#include "game_config.h"
#include "render_surface.h"
#include "types.h"
#include <cstdint>
#include <string_view>
#include <vector>

#include "bitmaps.h"
#include "font_renderer.h"

/*
 * Renderer: Takes care of rendering our RenderSurface to an actual device.
 */
class Renderer
{
  public:
    virtual ~Renderer() = default;
    virtual void SetSurfaceResolution(Size size) = 0;
    virtual Size GetSurfaceResolution() = 0;
    virtual void RenderFrame(const ScreenRenderSurface * surface) = 0;
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
 * BmpDecoder: Loads and parses BMP image from disk into in-memory image data
 */
class BmpDecoder
{
public:
    virtual ~BmpDecoder() = default;
    virtual ColorBitmap LoadRGBA(std::string_view relative_image_path) = 0;
    virtual MonoBitmap LoadGrayscale(std::string_view relative_image_path) = 0;
    virtual MonoBitmap LoadGrayscaleFromRGBA(std::string_view relative_image_path) = 0;
};

/*
 *  System: Represents the input/output system of the game - window, renderer, cursor and others in the future.
 *  We can afford the virtual calls here - none of this is going to be called too often.
 */
class GameSystem
{
  protected:
    
    ScreenRenderSurface render_surface; /* Final rendered picture, ready to be copied into VRAM */
  public:
    GameSystem(Size render_surface_size) : render_surface(render_surface_size) { }
    virtual ~GameSystem() = default;
    ScreenRenderSurface * GetSurface()
    {
        return &render_surface;
    } /* This should be effective, we'll be copying pixels left and right */

    virtual Renderer * GetRenderer() = 0;
    virtual Window * GetWindow() = 0;
    virtual Cursor * GetCursor() = 0;
    virtual BmpDecoder * GetBmpDecoder() = 0;
    virtual FontRenderer * GetFontRenderer() = 0;
};

inline std::unique_ptr<GameSystem> global_game_system;
std::unique_ptr<GameSystem> CreateGameSystem(VideoConfig video_config);
inline GameSystem * GetSystem() { return global_game_system.get(); }