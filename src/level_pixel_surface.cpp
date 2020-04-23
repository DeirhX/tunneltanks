#include "level_pixel_surface.h"

/* TODO: We're using color structures here because we started with Uint32 values
 *       and this was an easier transition. Eventually, all colors will be in a
 *       central array, and the pixel data will simply be 1-byte indexes. */

LevelPixelSurface::LevelPixelSurface(Size size) : size(size), default_color(0, 0, 0)
{
    pixel_data.resize(size.x * size.y);
}

void LevelPixelSurface::SetPixel(Position offset, Color color)
{
    if (offset.x < 0 || offset.y < 0 || offset.x >= size.x || offset.y >= size.y)
        return;
    auto & pixel_color = pixel_data[offset.y * size.x + offset.x];
    pixel_color = color.BlendWith<RenderedPixel>(pixel_color);
}

RenderedPixel LevelPixelSurface::GetPixel(Position offset)
{
    if (offset.x < 0 || offset.y < 0 || offset.x >= size.x || offset.y >= size.y)
        return this->default_color;
    return pixel_data[offset.y * size.x + offset.x];
}
