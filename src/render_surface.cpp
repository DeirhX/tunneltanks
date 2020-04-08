#include "render_surface.h"

#include "shape_renderer.h"

void Surface::Clear()
{
    for (auto & pixel : surface)
        pixel = {};
}

RenderedPixel Surface::GetPixel(const Position & position)
{
    if (this->use_default_color && (position.x < 0 || position.y < 0 || position.x >= size.x || position.y >= size.y))
        return this->default_color;
    return this->surface[position.x + position.y * size.x];
}

void Surface::SetPixel(Position position, Color color)
{
    assert(size.FitsInside(position.x, position.y));
    if (color.a == 0)
        return;
    else if (color.a == 255)
        surface[position.x + position.y * size.x] = RenderedPixel{color};
    else
        surface[position.x + position.y * size.x] = color.BlendWith(surface[position.x + position.y * size.x]);
}

void Surface::FillRectangle(Rect rect, Color color) {
    ShapeRenderer::FillRectangle(this, rect, color);
}

