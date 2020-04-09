#include "harvester.h"

#include "color_palette.h"
#include "game.h"
#include "projectile.h"
#include "shape_renderer.h"
#include "level.h"
#include "world.h"

void Harvester::Advance(Level * level)
{
    if (this->health <= 0)
    {
        Die(level);
    }
}

void Harvester::Draw(Surface * surface)
{
    ShapeRenderer::DrawCircle(surface, this->position, 4,
                              Palette.Get(Colors::ResourceInfoBackground), Palette.Get(Colors::ResourceInfoOutline));
}

void Harvester::Die(Level * level)
{
    this->is_alive = false;
    GetWorld()->GetProjectileList()->Add(ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                                            tweak::explosion::death::Speed,
                                                            tweak::explosion::death::Frames)
                                   .Explode<Shrapnel>(GetWorld()->GetLevel()));
}
