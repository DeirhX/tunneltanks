#pragma once

#include "types.h"

struct Color;
class Screen;

class ShapeRenderer
{
  public:
    static void DrawRectangle(Screen * screen, ScreenRect rect, bool round_corners, Color fill_color,
                              Color outline_color);
    static void DrawCircle(Screen * screen, ScreenPosition center, int radius, Color fill_color, Color outline_color);
};
