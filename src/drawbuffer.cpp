#include "base.h"
#include <drawbuffer.h>
#include <memory>

/* TODO: We're using color structures here because we started with Uint32 values
 *       and this was an easier transition. Eventually, all colors will be in a
 *       central array, and the pixel data will simply be 1-byte indexes. */

LevelDrawBuffer::LevelDrawBuffer(Size size) : size(size), default_color(0, 0, 0)
{
    pixel_data.resize(size.x * size.y);
}

void LevelDrawBuffer::SetPixel(Position offset, Color32 color)
{
    if (offset.x < 0 || offset.y < 0 || offset.x >= size.x || offset.y >= size.y)
        return;
    auto & pixel_color = pixel_data[offset.y * size.x + offset.x];
    pixel_color = color.BlendWith(pixel_color);
}

Color LevelDrawBuffer::GetPixel(Position offset)
{
    if (offset.x < 0 || offset.y < 0 || offset.x >= size.x || offset.y >= size.y)
        return this->default_color;
    return pixel_data[offset.y * size.x + offset.x];
}
