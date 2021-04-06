#pragma once
#include "color_palette.h"
#include "render_surface.h"

/*
 * WorldRenderSurfaces: Pixel surfaces for overlaying rarely modified terrain with often modified objects
 **/
struct WorldRenderSurfaces
{
    WorldRenderSurfaces(Size size) : terrain_surface(size, false), objects_surface(size, true) {}
    WorldRenderSurfaces(const WorldRenderSurfaces &) = delete;
    /* Holds rendered texture of the terrain, materializing each TerrainPixel into color */
    WorldRenderSurface terrain_surface;
    /* Holds a layer of frequently changed objects that will be drawn on top of terrain*/
    WorldRenderSurface objects_surface;

    void SetDimensions(const Size & size)
    {
        this->terrain_surface = WorldRenderSurface(size, false);
        this->objects_surface = WorldRenderSurface(size, true);
    }
};


/*
 * Renderer: Takes care of rendering our RenderSurface to an actual device.
 */
class Renderer
{
  public:
    Renderer(Size world_surface_size = {0, 0}) : render_surfaces(world_surface_size) {}

    virtual ~Renderer() {}
    virtual void SetSurfaceResolution(Size size) = 0;
    virtual Size GetSurfaceResolution() = 0;
    virtual void RenderFrame(const ScreenRenderSurface * surface) = 0;

    /* Note this will reinitialize the surface and throw everything away */
    void InitializeWorldSurfaces(Size dimensions);
    WorldRenderSurfaces & GetWorldSurfaces() { return this->render_surfaces; }
  private:
    

    /* Logical surfaces to composite */
    WorldRenderSurfaces render_surfaces;
};