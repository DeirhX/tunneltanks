#include "harvester.h"

#include "color_palette.h"
#include "game.h"
#include "projectile.h"
#include "shape_renderer.h"
#include "level.h"
#include "world.h"

Harvester::Harvester(Position position, HarvesterType type) : position(position), type(type)
{
}

void Harvester::Advance(Level * level)
{
    this->health = std::max(0, this->health);
    if (this->health == 0)
    {
        Die(level);
    }
}

void Harvester::Draw(Surface * surface) const
{
    ShapeRenderer::DrawCircle(surface, this->position, 4,
                              Palette.Get(Colors::ResourceInfoBackground), Palette.Get(Colors::ResourceInfoOutline));
}

void Harvester::AlterHealth(int shot_damage)
{
    this->health -= shot_damage;
}

void Harvester::Die(Level * level)
{
    this->is_alive = false;
    GetWorld()->GetProjectileList()->Add(ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                                            tweak::explosion::death::Speed,
                                                            tweak::explosion::death::Frames)
                                   .Explode<Shrapnel>(GetWorld()->GetLevel()));
}
