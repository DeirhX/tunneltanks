#include "machine.h"

#include "color_palette.h"
#include "terrain.h"
#include "terrain_algorithm.h"
#include "projectiles.h"
#include "shape_renderer.h"
#include "world.h"

Machine::Machine(Position position, Tank * owner, Reactor reactor_, BoundingBox bounding_box)
    : position(position), bounding_box(bounding_box), owner(owner), link_source(GetWorld(), position, LinkPointType::Machine), reactor(reactor_)
{
    
}

bool Machine::CheckAlive(Terrain * level)
{
    if (this->GetReactor().GetHealth() == 0)
    {
        Die(level);
        return false;
    }
    return true;
}

void Machine::SetState(MachineConstructState new_state)
{
    this->construct_state = new_state;
    if (new_state != MachineConstructState::Planted)
    {
        this->link_source.Disable();
    }
}

void Machine::SetPosition(Position new_position)
{
    this->position = new_position;
    this->link_source.UpdatePosition(new_position);
}

bool Machine::TestCollide(Position with_position) const
{
    return this->bounding_box.IsInside(with_position, this->position);
}

void Machine::SetIsTransported(bool new_value)
{
    this->is_transported = new_value;
    if (new_value)
    {   /* Transport */
        this->link_source.Disable();
        this->SetState(MachineConstructState::Transporting);
    }
    else
    {   /* Plant */
        this->link_source.Enable();
        this->SetState(MachineConstructState::Planted);
    }
}


void Harvester::Advance(Terrain * level)
{
    if (!CheckAlive(level))
        return;

    if (this->harvest_timer.AdvanceAndCheckElapsed())
    {

        auto is_suitable_position = [](Position tested_position) {
            TerrainPixel pixel = GetWorld()->GetTerrain()->GetPixel(tested_position);
            return Pixel::IsDirt(pixel);
        };
        /* Active algorithm: random pixel in radius. If full, first candidate on path to center from that position */
        std::optional<Position> suitable_pos = ShapeInspector::FromRandomPointInCircleToCenter(
            this->position, tweak::rules::HarvestMaxRange, is_suitable_position);

        /*auto closest_pixel = level::GetClosestPixel(GetWorld()->GetLevel()->GetLevelData(), this->position, tweak::rules::HarvestMaxRange,
                               [](TerrainPixel pixel) { return Pixel::IsDirt(pixel); });*/

        if (suitable_pos.has_value() && suitable_pos.value() != this->position)
        {
            GetWorld()->GetTerrain()->SetPixel(suitable_pos.value(), TerrainPixel::DirtGrow);
            if (this->owner)
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

void Harvester::Die(Terrain *)
{
    GetWorld()->GetProjectileList()->Add(
        ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                     tweak::explosion::death::Speed, tweak::explosion::death::Frames)
            .Explode<Shrapnel>(GetWorld()->GetTerrain()));

    this->Invalidate();
}

/*
 * Charger (Dodge): Charges empty pixels with collectable energy to be picked later
 */

void Charger::Advance(Terrain * level)
{
    if (!CheckAlive(level))
        return;
}

    //if (this->charge_timer.AdvanceAndCheckElapsed())
    //{
    //    auto is_suitable_position = [](Position tested_position)
    //    {
    //        TerrainPixel pixel = GetWorld()->GetLevel()->GetPixel(tested_position);
    //        return Pixel::IsEmpty(pixel) || Pixel::IsScorched(pixel) || Pixel::IsEnergy(pixel);
    //    };
    //    /* Active algorithm: random pixel in radius. If full, first candidate on path to center from that position */
    //    std::optional<Position> suitable_pos = ShapeInspector::FromRandomPointInCircleToCenter(
    //        this->position, tweak::rules::ChargeMaxRange, is_suitable_position);

    //    /*
    //     * Grow radially from center
    //    auto closest_pixel = level::GetClosestPixel(GetWorld()->GetLevel()->GetLevelData(), this->position,
    //                                                tweak::rules::HarvestMaxRange,
    //                                                [](TerrainPixel pixel) { return Pixel::IsEmpty(pixel) || Pixel::IsScorched(pixel); });
    //                                                */
    //    if (suitable_pos.has_value() && suitable_pos.value() != this->position)
    //    {
    //        TerrainPixel current_pixel = GetWorld()->GetLevel()->GetPixel(suitable_pos.value());
    //        TerrainPixel desired_pixel;
    //        if (current_pixel == TerrainPixel::EnergyLow)
    //            desired_pixel = TerrainPixel::EnergyMedium;
    //        else if (current_pixel == TerrainPixel::EnergyMedium || current_pixel == TerrainPixel::EnergyHigh)
    //            desired_pixel = TerrainPixel::EnergyHigh;
    //        else
    //            desired_pixel = TerrainPixel::EnergyLow;
    //        GetWorld()->GetLevel()->SetPixel(suitable_pos.value(), desired_pixel);
    //    }
    //}


void Charger::Draw(Surface * surface) const
{
    ShapeRenderer::DrawCircle(surface, this->position, 2, Palette.Get(Colors::HarvesterInside), Palette.Get(Colors::ChargerOutline));
}

void Charger::Die(Terrain *)
{
    GetWorld()->GetProjectileList()->Add(
        ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                     tweak::explosion::death::Speed, tweak::explosion::death::Frames)
            .Explode<Shrapnel>(GetWorld()->GetTerrain()));

    this->Invalidate();
}

/*
 * MachineTemplate
 */

bool MachineTemplate::PayCost() const
{
    return this->paying_container->Pay(this->build_cost);
}

MachineTemplate::MachineTemplate(Position position, BoundingBox bounding_box, MaterialAmount build_cost_,
                                 MaterialContainer & paying_host)
    : Machine{position, nullptr, Reactor{ReactorCapacity{}}, bounding_box}, build_cost(build_cost_), paying_container(&paying_host), origin_position{position}
{
    this->is_template = true;
    this->is_blocking_collidable = false;
}

void MachineTemplate::Advance(Terrain *) { this->is_available = this->paying_container->CanPay(this->build_cost); }

void MachineTemplate::ResetToOrigin() { this->SetPosition(this->origin_position); }



HarvesterTemplate::HarvesterTemplate(Position position, MaterialContainer & paying_host)
    : MachineTemplate{position, Harvester::bounding_box, tweak::rules::HarvesterCost, paying_host}
{
    this->link_source.Disable();
}

void HarvesterTemplate::Draw(Surface * surface) const
{
    Color outline_color = Palette.Get(Colors::HarvesterOutline);
    //outline_color.a = (outline_color.a * (this->IsAvailable() ? 128 : 32)) / 255;
    Color inside_color = Palette.Get(Colors::HarvesterInside);
    inside_color.a = (inside_color.a * (this->IsAvailable() ? 255 : 64)) / 255;
    ShapeRenderer::DrawFilledRectangle(surface, Rect{this->GetPosition() - Size{2, 2}, Size{5, 5}},
                                 true, inside_color, outline_color);
}

Machine * HarvesterTemplate::PayAndBuildMachine() const
{
    if (!this->PayCost())
        return nullptr;

    Machine & machine = GetWorld()->GetHarvesterList()->Emplace<Harvester>(this->position, this->type, this->owner);
    machine.SetState(MachineConstructState::Transporting);
    return &machine;
}

ChargerTemplate::ChargerTemplate(Position position, MaterialContainer & paying_host)
    : MachineTemplate{position, Charger::bounding_box, tweak::rules::ChargerCost, paying_host}
{
    this->link_source.Disable();
}

void ChargerTemplate::Draw(Surface * surface) const
{
    Color outline_color = Palette.Get(Colors::ChargerOutline);
    //outline_color.a = (outline_color.a * (this->IsAvailable() ? 128 : 32)) / 255;
    Color inside_color = Palette.Get(Colors::ChargerInside);
    inside_color.a = (inside_color.a * (this->IsAvailable() ? 255 : 64)) / 255;
    ShapeRenderer::DrawFilledRectangle(surface, Rect{this->GetPosition() - Size{2, 2}, Size{5, 5}}, true, inside_color,
                                       outline_color);
}

Machine * ChargerTemplate::PayAndBuildMachine() const
{
    if (!this->PayCost())
        return nullptr;

    Machine & machine = GetWorld()->GetHarvesterList()->Emplace<Charger>(this->position, this->owner);
    machine.SetState(MachineConstructState::Transporting);
    return &machine;
}

