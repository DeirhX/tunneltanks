#pragma once
#include "color.h"
#include "types.h"
#include <queue>
#include <vector>

/*
 * Surface: An array of raw (pixel) color information that can be effectively rendered
 *  into device video memory through a Renderer or to another Surface
 */
class Surface
{
 protected:
    std::vector<RenderedPixel> surface;
    Size size;

    bool use_default_color = false;
    RenderedPixel default_color;

    bool use_change_list = false;
    std::vector<Position> change_list;
  protected:
    Surface(Size size) : surface(size.x * size.y), size(size) {}
    RenderedPixel & At(Position position) { return surface[position.x + position.y * size.x]; }
  public:
    void Clear();
    void Resize(Size new_size) { surface.resize(new_size.x * new_size.y); }
    Size GetSize() const { return this->size; }

    /* Most basic draw functions only, for everything else, there is ShapeRenderer*/
    RenderedPixel GetPixel(const Position & position) const;
    void SetPixel(Position position, Color color);
    void FillRectangle(Rect rect, Color color);
    void OverlaySurface(const Surface * other); /* Combines surfaces using *only* source alpha channel */

    const std::vector<Position>& GetChangeList() { return this->change_list; }

    /* Default color will be used if out-of-bounds pixels are requested */
    Color GetDefaultColor() const { return default_color; }
    void SetDefaultColor(Color color)
    {
        this->default_color = color;
        this->use_default_color = true;
    }

    /* Raw access for GFX libraries with C interface to effectively copy it. Take great care not to overrun. */
    const RenderedPixel * GetRawData() { return &surface.front(); }
    int GetRowPitch() const { return this->size.x * sizeof(RenderedPixel); }
};

/*
 * ScreenRenderSurface: surface intended to represent the logical screen presented to player
 */
class ScreenRenderSurface : public Surface
{
  public:
    ScreenRenderSurface(Size size) : Surface(size) {}
    void SetPixel(ScreenPosition position, Color color) { Surface::SetPixel(Position{position}, color); }
    void FillRectangle(ScreenRect rect, Color color) { Surface::FillRectangle(Rect{Position{rect.pos}, rect.size}, color); }
};


/*
 * WorldRenderSurface: surface intended to represent layers in the game world (level)
 */
class WorldRenderSurface : public Surface
{
    std::queue<Position> change_list;
  public:
    WorldRenderSurface(Size size, bool use_change_list) : Surface(size) { this->use_change_list = use_change_list; }
    void SetPixel(Position position, Color color) { Surface::SetPixel(position, color); }
    void FillRectangle(Rect rect, Color color) { Surface::FillRectangle(rect, color); }
};
