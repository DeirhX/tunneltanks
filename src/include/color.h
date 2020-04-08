#pragma once
#include <cstdint>

struct ColorLayout
{
    /* The order is actually important - if it's identical to memory layout of our render surface, it's just a memcopy*/
    uint8_t b{}, g{}, r{}, a{};

    ColorLayout() = default;
    ColorLayout(unsigned char r, unsigned char g, unsigned char b, unsigned char a = 255) : r(r), g(g), b(b), a(a) {}
};

struct Color : public ColorLayout
{

  public:
    Color() = default;
    Color(unsigned char r, unsigned char g, unsigned char b, unsigned char a = 255) : ColorLayout(r, g, b, a) {}
    Color Mask(Color other) const
    {
        return Color((this->r * other.r) / 255, (this->g * other.g) / 255, (this->b * other.b) / 255,
                       (this->a * other.a) / 255);
    }
    template <typename PixelDataType = Color>
    PixelDataType BlendWith(PixelDataType other) const
    {
        if (a == 0)
            return other;
        if (a == 255)
            return PixelDataType(r, g, b);
        return PixelDataType((this->r * a) / 255 + other.r * (255 - a) / 255,
                             (this->g * a) / 255 + other.g * (255 - a) / 255,
                             (this->b * a) / 255 + other.b * (255 - a) / 255);
    }
};

/*
 * RenderedPixel: Possibly an exact memory layout of a pixel that's going to be directly copied into video memory.
 *   If matched exactly, no conversion will be needed and we can copy entire array from RAM into VRAM.
 */
struct RenderedPixel : public Color
{
    RenderedPixel() = default;
    RenderedPixel(unsigned char r, unsigned char g, unsigned char b, unsigned char a = 255) : Color(r, g, b, a) {}
    RenderedPixel(Color color) : Color(color) {}
};
