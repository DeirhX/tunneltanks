#include "render_surface.h"

#include "shape_renderer.h"

void Surface::Clear()
{
    //for (auto & pixel : surface)
    //    pixel = {};
    std::memset(&this->surface.front(), 0, sizeof(RenderedPixel) * this->surface.size());

    if (this->use_change_list)
        this->change_list.clear();
}

RenderedPixel Surface::GetPixel(const Position & position) const
{
    if (this->use_default_color && (position.x < 0 || position.y < 0 || position.x >= size.x || position.y >= size.y))
        return this->default_color;
    return this->surface[position.x + position.y * size.x];
}

void Surface::SetPixel(Position position, Color color)
{
    assert(size.FitsInside(position.x, position.y));
    if (color.a == 255)
        surface[position.x + position.y * size.x] = RenderedPixel{color};
    else if (color.a == 0)
        return;
    else
        surface[position.x + position.y * size.x] = color.BlendWith(surface[position.x + position.y * size.x]);

    if (this->use_change_list)
        this->change_list.emplace_back(position);
}

void Surface::FillRectangle(Rect rect, Color color) {
    ShapeRenderer::FillRectangle(this, rect, color);
}

void Surface::OverlaySurface(const Surface * other)
{
    assert(other->size == this->size);
    if (other->use_change_list)
    {
        for (const Position& pos : other->change_list)
        {
            SetPixel(pos, other->GetPixel(pos));
        }
    }
    else
    {
        for (size_t i = 0; i < this->surface.size(); ++i)
        {
            this->surface[i] = other->surface[i].BlendWith(this->surface[i]);
        }
    }
}

