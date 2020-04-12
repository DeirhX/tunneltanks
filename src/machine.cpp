#include "machine.h"

#include "color_palette.h"
#include "game.h"
#include "level.h"
#include "projectile.h"
#include "shape_renderer.h"
#include "level_algorithm.h"
#include "world.h"

bool Machine::CheckAlive(Level * level)
{
    this->health = std::max(0, this->health);
    if (this->health == 0)
    {
        Die(level);
        return false;
    }
    return true;
}

void Machine::AlterHealth(int shot_damage) { this->health -= shot_damage; }

void Harvester::Advance(Level * level)
{
    if (!CheckAlive(level))
        return;

    if (!this->harvest_timer.AdvanceAndCheckElapsed())
    {
        auto closest_pixel = level::GetClosestPixel(GetWorld()->GetLevel()->GetLevelData(), this->position, tweak::rules::HarvestMaxRange,
                               [](LevelPixel pixel) { return Pixel::IsDirt(pixel); });

        if (closest_pixel.has_value() && closest_pixel.value() != this->position)
        {
            GetWorld()->GetLevel()->SetPixel(closest_pixel.value(), LevelPixel::Blank);
            this->owner->GetResources().AddDirt(1);
        }
    }
}

void Harvester::Draw(Surface * surface) const
{
    ShapeRenderer::DrawCircle(surface, this->position, 2, Palette.Get(Colors::HarvesterInside),
                              Palette.Get(Colors::HarvesterOutline));
}

bool Harvester::IsColliding(Position with_position) const
{
    return this->bounding_box.IsInside(with_position, this->position);
}

void Harvester::Die(Level * level)
{
    this->is_alive = false;
    GetWorld()->GetProjectileList()->Add(
        ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                     tweak::explosion::death::Speed, tweak::explosion::death::Frames)
            .Explode<Shrapnel>(GetWorld()->GetLevel()));
}


/*
 * Charger (Dodge): Charges empty pixels with collectable energy to be picked later
 */

void Charger::Advance(Level * level)
{
    if (!CheckAlive(level))
        return;

    if (!this->charge_timer.AdvanceAndCheckElapsed())
    {
        auto closest_pixel = level::GetClosestPixel(GetWorld()->GetLevel()->GetLevelData(), this->position,
                                                    tweak::rules::HarvestMaxRange,
                                                    [](LevelPixel pixel) { return Pixel::IsEmpty(pixel); });

        if (closest_pixel.has_value() && closest_pixel.value() != this->position)
        {
            GetWorld()->GetLevel()->SetPixel(closest_pixel.value(), LevelPixel::Energy);
        }
    }
}

void Charger::Draw(Surface * surface) const
{
    ShapeRenderer::DrawCircle(surface, this->position, 2, Palette.Get(Colors::HarvesterInside),
                              Palette.Get(Colors::ChargerOutline));
}

bool Charger::IsColliding(Position with_position) const
{
    return this->bounding_box.IsInside(with_position, this->position);
}

void Charger::Die(Level * level)
{
    this->is_alive = false;
    GetWorld()->GetProjectileList()->Add(
        ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                     tweak::explosion::death::Speed, tweak::explosion::death::Frames)
            .Explode<Shrapnel>(GetWorld()->GetLevel()));
}

