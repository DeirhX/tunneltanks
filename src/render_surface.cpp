#include "render_surface.h"

void RenderSurface::DrawPixel(NativeScreenPosition position, RenderedPixel color)
{
    assert(size.FitsInside(position.x, position.y));
    surface[position.x + position.y * size.x] = color;
}

void RenderSurface::DrawRectangle(NativeRect rect, RenderedPixel color)
{
    NativeScreenPosition pos;
    for (pos.x = rect.Left(); pos.x < rect.Right(); ++pos.x)
        for (pos.y = rect.Top(); pos.y < rect.Bottom(); ++pos.y)
            DrawPixel(pos, color);
}
