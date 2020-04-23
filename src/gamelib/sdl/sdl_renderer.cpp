#include "sdl_renderer.h"
#include "exceptions.h"
#include "require_sdl.h"
#include "tweak.h"
#include "types.h"
#include <memory>
#include <sdl2/include/SDL.h>

/*
 *  SdlRenderer wrapper over SDL_Renderer, SDL_Texture and SDL_Surface
 */

SdlRenderer::SdlRenderer(SdlWindow * owning_window, ScreenRenderSurface * render_surface)
    : render_surface(render_surface)
{
    Recreate(owning_window);
}

void SdlRenderer::SetSurfaceResolution(Size size)
{
    this->render_surface->Resize(size);
    Recreate(this->owning_window);
}

void SdlRenderer::RenderFrame(const ScreenRenderSurface * surface)
{
    if (SDL_UpdateTexture(this->native_texture.get(), nullptr, this->render_surface->GetRawData(),
                          this->render_surface->GetRowPitch()))
    {
        throw RenderException("Update texture failed", SDL_GetError());
    }
    if (SDL_RenderClear(this->native_renderer.get()))
    {
        throw RenderException("Render clear failed", SDL_GetError());
    }
    if (SDL_RenderCopy(this->native_renderer.get(), this->native_texture.get(), nullptr, nullptr))
    {
        throw RenderException("Render copy failed", SDL_GetError());
    }
    SDL_RenderPresent(this->native_renderer.get());
}

void SdlRenderer::Recreate(SdlWindow * new_owning_window)
{
    this->owning_window = new_owning_window;
    this->native_renderer.reset();
    this->native_texture.reset();

    this->native_renderer = {
        SDL_CreateRenderer(owning_window->GetNativeWindow(), -1, SDL_RENDERER_ACCELERATED),
        [](SDL_Renderer * renderer) { /* SDL_DestroyRenderer(renderer);*/ /* Destroyed by window */ }};
    if (!this->native_renderer)
        throw GameInitException("Failed to create game renderer.");
    if (SDL_RenderSetLogicalSize(this->native_renderer.get(), this->render_surface->GetSize().x,
                                 this->render_surface->GetSize().y))
        throw GameInitException("Failed to set logical size.");

    this->native_texture = {
        SDL_CreateTexture(this->native_renderer.get(), SDL_PIXELFORMAT_ARGB8888, SDL_TEXTUREACCESS_STREAMING,
                          this->render_surface->GetSize().x, this->render_surface->GetSize().y),
        [](SDL_Texture * texture) { /* SDL_DestroyTexture(texture); */ /* Destroyed by the surface */ }};
    if (!this->native_texture)
        throw GameInitException("Failed to create texture to render into.");

    /* Our cursor is set to nothing, this will hide it */
    SDL_ShowCursor(SDL_ENABLE);
}

/*
 * SdlWindow
 */

SdlWindow::SdlWindow(Size size, bool is_fullscreen) : window_size(size), is_fullscreen(is_fullscreen)
{
    Resize(this->window_size, this->is_fullscreen);
}

void SdlWindow::AttachRenderer(SdlRenderer * new_renderer)
{
    assert(!this->renderer);
    this->renderer = new_renderer;
    //this->renderer->Recreate(this);
}

void SdlWindow::Resize(Size size, bool fullscreen)
{
    this->native_window.reset();
    if (is_fullscreen)
    {
        this->native_window = {SDL_CreateWindow(tweak::system::WindowTitle, SDL_WINDOWPOS_UNDEFINED,
                                                SDL_WINDOWPOS_UNDEFINED, 0, 0, SDL_WINDOW_FULLSCREEN_DESKTOP),
                               [](SDL_Window * window) { SDL_DestroyWindow(window); }};
    }
    else
    {
        this->native_window = {SDL_CreateWindow(tweak::system::WindowTitle, SDL_WINDOWPOS_CENTERED,
                                                SDL_WINDOWPOS_CENTERED, size.x, size.y, SDL_WINDOW_RESIZABLE),
                               [](SDL_Window * window) { SDL_DestroyWindow(window); }};
    }

    if (!this->native_window)
        throw GameInitException("Failed to create fullscreen window.");

    this->window_size = size;
    this->is_fullscreen = fullscreen;
    if (this->renderer)
        this->renderer->Recreate(this);
}

void SdlCursor::Hide()
{
    int32_t cursorData[2] = {0, 0};
    this->native_cursor = holder_with_deleter<SDL_Cursor>(
        SDL_CreateCursor(reinterpret_cast<Uint8 *>(cursorData), reinterpret_cast<Uint8 *>(cursorData), 8, 8, 4, 4),
        [](SDL_Cursor * cursor) { SDL_FreeCursor(cursor); });
    SDL_SetCursor(this->native_cursor.get());
    SDL_ShowCursor(SDL_DISABLE);
}

void SdlCursor::Show()
{
    this->native_cursor.reset();

    SDL_SetCursor(nullptr);
    SDL_ShowCursor(SDL_ENABLE);
}
