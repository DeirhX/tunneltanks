#pragma once
#include "gamelib/sdl/sdl_renderer.h"
#include "types.h"
#include <vector>

class LevelPixelSurface
{
    std::vector<RenderedPixel> pixel_data;
    Size size;
    RenderedPixel default_color;

  public:
    LevelPixelSurface(Size size);
    Color GetDefaultColor() { return default_color; }
    void SetDefaultColor(Color color) { default_color = color; }
    void SetPixel(Position pos, Color color);
    RenderedPixel GetPixel(Position pos);
};
