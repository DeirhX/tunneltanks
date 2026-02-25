#include "pch.h"
#include "renderer.h"

#include "color_palette.h"
namespace crust
{

void Renderer::InitializeWorldSurfaces(Size dimensions)
{
    this->render_surfaces.SetDimensions(dimensions);
    render_surfaces.terrain_surface.SetDefaultColor(static_cast<Color>(Palette.Get(Colors::Rock)));
    render_surfaces.objects_surface.SetDefaultColor({});
}

} // namespace MyNamespace