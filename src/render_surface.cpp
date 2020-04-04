#include "render_surface.h"

void RenderSurface::DrawPixel(NativeScreenPosition position, Color color)
{
    assert(size.FitsInside(position.x, position.y));
    if (color.a == 0)
        return;
    else if (color.a == 255)
        surface[position.x + position.y * size.x] = RenderedPixel{color};
    else
        surface[position.x + position.y * size.x] = color.BlendWith(surface[position.x + position.y * size.x]);
}

void RenderSurface::DrawRectangle(NativeRect rect, Color color)
{
    NativeScreenPosition pos;
    for (pos.x = rect.Left(); pos.x < rect.Right(); ++pos.x)
        for (pos.y = rect.Top(); pos.y < rect.Bottom(); ++pos.y)
            DrawPixel(pos, color);
}
