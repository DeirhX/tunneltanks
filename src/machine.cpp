#include "machine.h"

#include "color_palette.h"
#include "game.h"
#include "level.h"
#include "projectile.h"
#include "shape_renderer.h"
#include "level_algorithm.h"
#include "raycaster.h"
#include "world.h"

Machine::Machine(Position position, Tank * owner, Reactor reactor_, BoundingBox bounding_box)
    : position(position), bounding_box(bounding_box), owner(owner), link_source(GetWorld(), position, LinkPointType::Machine), reactor(reactor_)
{
    
}

bool Machine::CheckAlive(Level * level)
{
    if (this->GetReactor().GetHealth() == 0)
    {
        Die(level);
        return false;
    }
    return true;
}

void Harvester::Advance(Level * level)
{
    if (!CheckAlive(level))
        return;

    if (this->harvest_timer.AdvanceAndCheckElapsed())
    {

        auto is_suitable_position = [](Position tested_position) {
            LevelPixel pixel = GetWorld()->GetLevel()->GetPixel(tested_position);
            return Pixel::IsDirt(pixel);
        };
        /* Active algorithm: random pixel in radius. If full, first candidate on path to center from that position */
        std::optional<Position> suitable_pos = ShapeInspector::FromRandomPointInCircleToCenter(
            this->position, tweak::rules::HarvestMaxRange, is_suitable_position);

        /*auto closest_pixel = level::GetClosestPixel(GetWorld()->GetLevel()->GetLevelData(), this->position, tweak::rules::HarvestMaxRange,
                               [](LevelPixel pixel) { return Pixel::IsDirt(pixel); });*/

        if (suitable_pos.has_value() && suitable_pos.value() != this->position)
        {
            GetWorld()->GetLevel()->SetPixel(suitable_pos.value(), LevelPixel::DirtGrow);
            this->owner->GetResources().Add(1_dirt);
        }
    }

    this->link_source.UpdatePosition(this->position);
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
    GetWorld()->GetProjectileList()->Add(
        ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                     tweak::explosion::death::Speed, tweak::explosion::death::Frames)
            .Explode<Shrapnel>(GetWorld()->GetLevel()));

    this->Invalidate();
}


/*
 * Charger (Dodge): Charges empty pixels with collectable energy to be picked later
 */

void Charger::Advance(Level * level)
{
    if (!CheckAlive(level))
        return;


    //if (this->charge_timer.AdvanceAndCheckElapsed())
    //{
    //    auto is_suitable_position = [](Position tested_position)
    //    {
    //        LevelPixel pixel = GetWorld()->GetLevel()->GetPixel(tested_position);
    //        return Pixel::IsEmpty(pixel) || Pixel::IsScorched(pixel) || Pixel::IsEnergy(pixel);
    //    };
    //    /* Active algorithm: random pixel in radius. If full, first candidate on path to center from that position */
    //    std::optional<Position> suitable_pos = ShapeInspector::FromRandomPointInCircleToCenter(
    //        this->position, tweak::rules::ChargeMaxRange, is_suitable_position);

    //    /*
    //     * Grow radially from center
    //    auto closest_pixel = level::GetClosestPixel(GetWorld()->GetLevel()->GetLevelData(), this->position,
    //                                                tweak::rules::HarvestMaxRange,
    //                                                [](LevelPixel pixel) { return Pixel::IsEmpty(pixel) || Pixel::IsScorched(pixel); });
    //                                                */
    //    if (suitable_pos.has_value() && suitable_pos.value() != this->position)
    //    {
    //        LevelPixel current_pixel = GetWorld()->GetLevel()->GetPixel(suitable_pos.value());
    //        LevelPixel desired_pixel;
    //        if (current_pixel == LevelPixel::EnergyLow)
    //            desired_pixel = LevelPixel::EnergyMedium;
    //        else if (current_pixel == LevelPixel::EnergyMedium || current_pixel == LevelPixel::EnergyHigh)
    //            desired_pixel = LevelPixel::EnergyHigh;
    //        else
    //            desired_pixel = LevelPixel::EnergyLow;
    //        GetWorld()->GetLevel()->SetPixel(suitable_pos.value(), desired_pixel);
    //    }
    //}
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
    GetWorld()->GetProjectileList()->Add(
        ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                     tweak::explosion::death::Speed, tweak::explosion::death::Frames)
            .Explode<Shrapnel>(GetWorld()->GetLevel()));

    this->Invalidate();
}

