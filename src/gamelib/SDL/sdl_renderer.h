#pragma once
#include "SDL.h"
#include "gamelib.h"

class SdlWindow;

class SdlRenderer final : public Renderer
{
    /* Native SDL data */
    holder_with_deleter<SDL_Renderer> native_renderer = {};
    holder_with_deleter<SDL_Texture> native_texture = {};
    holder_with_deleter<SDL_Surface> native_surface = {};

    Size surface_size = {};
    SdlWindow * owning_window = nullptr;

  public:
    explicit SdlRenderer(SdlWindow * owning_window, Size surface_size);
    void DrawPixel(NativeScreenPosition position, Color32 color) override;
    void DrawRectangle(NativeRect rect, Color32 color) override;
    void SetSurfaceResolution(Size size) override;
    Size GetSurfaceResolution() override { return this->surface_size; }
    void RenderFrame() override;
  public:
    void Recreate(SdlWindow * new_owning_window);
};

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
    //SdlRenderer * GetRenderer() override { return this->renderer; }
    void Resize(Size size, bool is_fullscreen) override;
};

class SdlCursor final : public Cursor
{
    holder_with_deleter<SDL_Cursor> native_cursor = {};

  public:
    void Hide() override;
    void Show() override;
};