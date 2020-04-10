#pragma once

#include "containers.h"
#include "render_surface.h"
#include "types.h"

struct Color;
class Screen;

class ShapeRenderer
{
  public:
    static void FillRectangle(Surface * surface, Rect rect, Color color);
    static void DrawRectangle(Surface * screen, Rect rect, bool round_corners, Color fill_color,
                              Color outline_color);
    template <typename ValueType, typename InspectFunc>
    static void InspectRectangle(const Container2D<ValueType> & container, Rect rect, InspectFunc inspect_func);

    static void DrawCircle(Surface * screen, Position center, int radius, Color fill_color, Color outline_color);
};

/* InspectFunc - return true to continue inspection, false to terminate */
template <typename ValueType, typename InspectFunc>
void ShapeRenderer::InspectRectangle(const Container2D<ValueType>& container, Rect rect, InspectFunc inspect_func)
{
    for (int x = rect.Left(); x <= rect.Right(); ++x)
        for (int y = rect.Top(); y <= rect.Bottom(); ++y)
        {
            /* Are we inside edges?  */
            if (x != rect.Left() && x != rect.Right() && y != rect.Top() && y != rect.Bottom())
                continue;
            Position pos = {x, y};
            if (!inspect_func(pos, container[pos]))
                return;
        }
}
