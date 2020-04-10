#pragma once

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
    //template <typename InspectFunc>
    //static void InspectRectangle(  InspectFunc inspect_func)
    static void DrawCircle(Surface * screen, Position center, int radius, Color fill_color, Color outline_color);
};
