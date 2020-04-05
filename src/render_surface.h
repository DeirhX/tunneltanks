#pragma once
#include <vector>


#include "color.h"
#include "types.h"



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
    void DrawPixel(ScreenPosition position, Color color);
    void DrawRectangle(ScreenRect rect, Color color);
    Size GetSize() const { return this->size; }

    /* Raw access for GFX libraries with C interface to effectively copy it. Take great care not to overrun. */
    const RenderedPixel * GetRawData() { return &surface.front(); }
    int GetRowPitch() const { return this->size.x * sizeof(RenderedPixel); }
};
