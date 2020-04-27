#include "sprite.h"

#include "color_palette.h"
#include "shape_renderer.h"

void Sprite::Draw(Surface & surface) const {}

void FailedInteraction::Advance(Level * level)
{
    if (this->destroy_timer.AdvanceAndCheckElapsed())
    {
        Invalidate();
    }
}

void FailedInteraction::Draw(Surface & surface) const
{
    constexpr int diam = 3;
    ShapeRenderer::DrawLine(surface, this->GetPosition() + Offset{-diam, -diam},
                            this->GetPosition() + Offset{diam, diam}, Palette.Get(Colors::FailedInteraction));
    ShapeRenderer::DrawLine(surface, this->GetPosition() + Offset{+diam, -diam},
                            this->GetPosition() + Offset{-diam, diam}, Palette.Get(Colors::FailedInteraction));
}
