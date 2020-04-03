#pragma once
#include <cstdint>
#include <vector>
#include "game_config.h"
#include "types.h"


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