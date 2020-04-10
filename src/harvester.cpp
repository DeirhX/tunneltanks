#include "harvester.h"

#include "color_palette.h"
#include "game.h"
#include "projectile.h"
#include "shape_renderer.h"
#include "level.h"
#include "world.h"

Harvester::Harvester(Position position, HarvesterType type, Tank * owner) : position(position), type(type), owner(owner)
{
}

void Harvester::Advance(Level * level)
{
    this->health = std::max(0, this->health);
    if (this->health == 0)
    {
        Die(level);
        return;
    }

    if (!this->harvest_timer.AdvanceAndCheckElapsed())
    {
        auto check_pixel = [this](Position pos, const LevelPixel & pixel) {
            if (Pixel::IsDirt(pixel))
            {
                GetWorld()->GetLevel()->SetPixel(pos, LevelPixel::Blank);
                this->owner->GetResources().AddDirt(1);
                return false;
            }
            return true;
        };

        for (int i = 1; i < tweak::rules::HarvestMaxRange; ++i)
        {
            ShapeRenderer::InspectRectangle(GetWorld()->GetLevel()->GetLevelData(),
                                            Rect{this->position.x - i, this->position.y - i, i*2 + 1, i*2 + 1}, check_pixel);
        }
    }
}

void Harvester::Draw(Surface * surface) const
{
    ShapeRenderer::DrawCircle(surface, this->position, 4,
                              Palette.Get(Colors::HarvesterInside), Palette.Get(Colors::HarvesterOutline));
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
