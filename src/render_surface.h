#pragma once
#include <vector>

#include "types.h"


/*
 * RenderedPixel: Possibly an exact memory layout of a pixel that's going to be directly copied into video memory.
 *   If matched exactly, no conversion will be needed and we can copy entire array from RAM into VRAM.
 */
struct RenderedPixel
{
    uint8_t r;
    uint8_t g;
    uint8_t b;
    uint8_t a;

    RenderedPixel() = default;
    RenderedPixel(uint8_t r, uint8_t g, uint8_t b) : r(r), g(g), b(b), a(255) {}
    RenderedPixel(uint8_t r, uint8_t g, uint8_t b, uint8_t a) : r(r), g(g), b(b), a(a) {}
    RenderedPixel(Color color) : r(color.r), g(color.g), b(color.b), a(255) {}
    RenderedPixel(Color32 color) : r(color.r), g(color.g), b(color.b), a(color.a) {}

    operator Color() const { return Color{this->r, this->g, this->b}; }
    operator Color32() const { return Color32{this->r, this->g, this->b, this->a}; }
};

/*
 * RenderSurface: An array of raw (pixel) color information that can
 *  be effectively rendered into device video memory through a Renderer
 */
class RenderSurface
{
    std::vector<RenderedPixel> surface;
    Size size;

  public:
    RenderSurface(Size size) : surface(size.x * size.y), size(size) {}
    void Reset()
    {
        for (auto & pixel : surface)
            pixel = {0, 0, 0, 0};
    }
    void Resize(Size new_size) { surface.resize(new_size.x * new_size.y); }
    void DrawPixel(NativeScreenPosition position, RenderedPixel color);
    void DrawRectangle(NativeRect rect, RenderedPixel color);
    Size GetSize() const { return this->size; }

    /* Raw access for GFX libraries with C interface to effectively copy it. Take great care not to overrun. */
    const RenderedPixel * GetRawData() { return &surface.front(); }
    int GetRowPitch() const { return this->size.x * sizeof(RenderedPixel); }
};
