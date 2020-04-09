#include "harvester.h"

#include "color_palette.h"
#include "shape_renderer.h"

void Harvester::Advance(Level * level)
{
}

void Harvester::Draw(Surface * surface)
{
    ShapeRenderer::DrawCircle(surface, this->position, 4,
                              Palette.Get(Colors::ResourceInfoBackground), Palette.Get(Colors::ResourceInfoOutline));
}
