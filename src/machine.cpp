#include "machine.h"

#include "color_palette.h"
#include "game.h"
#include "level.h"
#include "projectile.h"
#include "shape_renderer.h"
#include "world.h"

void Machine::Advance(Level * level)
{
    this->health = std::max(0, this->health);
    if (this->health == 0)
    {
        Die(level);
        return;
    }
}

void Machine::AlterHealth(int shot_damage) { this->health -= shot_damage; }

void Harvester::Advance(Level * level)
{
    Base::Advance(level);

    if (!this->harvest_timer.AdvanceAndCheckElapsed())
    {
        float nearest_distance = std::numeric_limits<float>::max();
        Position nearest_pos = this->position;

        auto check_pixel = [this, &nearest_distance, &nearest_pos](Position pos, const LevelPixel & pixel) {
            if (Pixel::IsDirt(pixel))
            {
                float distance = (pos - this->position).GetSize();
                if (distance <= tweak::rules::HarvestMaxRange && nearest_distance > distance)
                {
                    nearest_pos = pos;
                    nearest_distance = distance;
                }
            }
            return true;
        };

        for (int i = 1; i < tweak::rules::HarvestMaxRange; ++i)
        {
            ShapeRenderer::InspectRectangle(GetWorld()->GetLevel()->GetLevelData(),
                                            Rect{this->position.x - i, this->position.y - i, i * 2 + 1, i * 2 + 1},
                                            check_pixel);
            if (float(i) >= nearest_distance)
                break;
        }

        if (nearest_pos != this->position)
        {
            GetWorld()->GetLevel()->SetPixel(nearest_pos, LevelPixel::Blank);
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
{ Base::Advance(level); }

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

