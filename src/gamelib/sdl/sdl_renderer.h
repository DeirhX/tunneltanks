#pragma once
#include "game_system.h"
#include <sdl2/include/SDL.h>

class SdlWindow;

/* Renderer implemented in SDL2 */
class SdlRenderer final : public Renderer
{
    /* Native SDL data */
    holder_with_deleter<SDL_Renderer> native_renderer = {};
    holder_with_deleter<SDL_Texture> native_texture = {};

    ScreenRenderSurface * render_surface = {};
    SdlWindow * owning_window = nullptr;

  public:
    explicit SdlRenderer(SdlWindow * owning_window, ScreenRenderSurface * render_surface);
    void SetSurfaceResolution(Size size) override;
    Size GetSurfaceResolution() override { return this->render_surface->GetSize(); }
    void RenderFrame(const ScreenRenderSurface * surface) override;

  public:
    void Recreate(SdlWindow * new_owning_window);
};

/* Native window implemented in SDL2 */
class SdlWindow final : public Window
{
    /* Native SDL data */
    holder_with_deleter<SDL_Window> native_window = {};

    SdlRenderer * renderer = nullptr;
    Size window_size = {};
    bool is_fullscreen = false;

  public:
    SdlWindow(Size size, bool is_fullscreen);
    SDL_Window * GetNativeWindow() { return native_window.get(); }
    void AttachRenderer(SdlRenderer * renderer);

    bool IsFullscreen() override { return this->is_fullscreen; }
    Size GetResolution() override { return this->window_size; }
    void Resize(Size size, bool is_fullscreen) override;
};

/* Native cursor implemented in SDL2 */
class SdlCursor final : public Cursor
{
    holder_with_deleter<SDL_Cursor> native_cursor = {};

  public:
    void Hide() override;
    void Show() override;
};