#include "sprite.h"


#include "color_palette.h"
#include "shape_renderer.h"

void Sprite::Draw(Surface * surface) const
{
}

void FailedInteraction::Advance(Level * level)
{
    if (this->destroy_timer.AdvanceAndCheckElapsed())
    {
        Invalidate();
    }
}

void FailedInteraction::Draw(Surface * surface) const
{
    ShapeRenderer::DrawLine(surface, this->GetPosition() + Offset{-5, -5}, this->GetPosition() + Offset{5, 5},
                            Palette.Get(Colors::FailedInteraction));
    ShapeRenderer::DrawLine(surface, this->GetPosition() + Offset{+5, -5}, this->GetPosition() + Offset{-5, 5},
                            Palette.Get(Colors::FailedInteraction));
}
