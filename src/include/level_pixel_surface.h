#pragma once
#include <memory>
#include <types.h>
#include <vector>

#include "sdl_renderer.h"

class LevelPixelSurface
{
    std::vector<RenderedPixel> pixel_data;
    Size size;
    RenderedPixel default_color;

  public:
    LevelPixelSurface(Size size);
    Color GetDefaultColor() { return default_color; }
    void SetDefaultColor(Color color) { default_color = color; }
    void SetPixel(Position pos, Color32 color);
    RenderedPixel GetPixel(Position pos);
};
