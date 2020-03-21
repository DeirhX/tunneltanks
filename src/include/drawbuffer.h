#pragma once
#include <memory>
#include <types.h>
#include <vector>

class LevelDrawBuffer
{
    std::vector<Color> pixel_data;
    Size size;
    Color default_color;

  public:
    LevelDrawBuffer(Size size);
    Color & DefaultColor() { return default_color; }
    void SetDefaultColor(Color color) { default_color = color; }
    void SetPixel(Position pos, Color32 color);
    Color GetPixel(Position pos);
};
