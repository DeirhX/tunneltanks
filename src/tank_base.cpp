#include "tank_base.h"
#include "shape_renderer.h"
#include "world.h"

TankBase::TankBase(Position position, TankColor color) : position(position), color(color) {}

void TankBase::BeginGame()
{
    CreateMachineTemplates(GetWorld());
    RegisterLinkPoint(GetWorld());
}

void TankBase::RegisterLinkPoint(World * world)
{
    assert(!this->link_point);
    this->link_point = world->GetLinkMap()->RegisterLinkPoint(this->position, LinkPointType::Base);
}

void TankBase::CreateMachineTemplates(World * world)
{
    Position left_center = (this->position - Size{TankBase::BaseSize.x / 2, 0});
    this->base_charger_template = &world->GetHarvesterList()->Emplace<ChargerTemplate>(
        left_center + Size{Charger::bounding_box.size.x / 2 + 2, 0});

    Position right_center = (this->position + Size{TankBase::BaseSize.x / 2, 0});
    this->base_harvester_template = &world->GetHarvesterList()->Emplace<HarvesterTemplate>(
        right_center - Size{Charger::bounding_box.size.x / 2 + 2, 0});
}

bool TankBase::IsInside(Position tested_position) const
{
    return this->bounding_box.IsInside(tested_position, this->position);
}
void TankBase::AbsorbResources(MaterialContainer & other) { this->materials.Absorb(other); }

void TankBase::AbsorbResources(MaterialContainer & other, MaterialAmount rate)
{
    /* Absorb a maximum of rate limit from source */
    MaterialContainer absorber = {MaterialCapacity{rate}};
    absorber.Absorb(other);
    /* Give it over to us, possibly keeping any left-over remainder */
    this->materials.Absorb(absorber);
    /* Return the left-over to original source*/
    other.Absorb(absorber);
}

void TankBase::GiveResources(MaterialContainer & other, MaterialAmount rate)
{
    /* Absorb a maximum of rate limit from source */
    MaterialContainer absorber = {MaterialCapacity{rate}};
    absorber.Absorb(this->materials);
    /* Give it over to us, possibly keeping any left-over remainder */
    other.Absorb(absorber);
    /* Return the left-over to original source*/
    this->materials.Absorb(absorber);
}

void TankBase::GiveResources(Reactor & other, ReactorState rate)
{
    /* Absorb a maximum of rate limit from source */
    Reactor absorber = {ReactorCapacity{rate}};
    absorber.Absorb(this->reactor);
    /* Give it over to us, possibly keeping any left-over remainder */
    other.Absorb(absorber);
    /* Return the left-over to original source*/
    this->reactor.Absorb(absorber);
}

void TankBase::RechargeTank(Tank * tank)
{
    if (tank->GetColor() == this->GetColor())
    {
        this->GiveResources(tank->GetReactor(), {tweak::tank::HomeChargeSpeed, tweak::tank::HomeHealSpeed});
    }
    else
        this->GiveResources(tank->GetReactor(), {tweak::tank::EnemyChargeSpeed, tweak::tank::EnemyHealSpeed});
}

void TankBase::Draw(Surface * surface) const
{
    /* Energy layer */
    Size materials_rect_size = BaseSize + Size{2, 2};
    Rect materials_rect = Rect{Position{this->position - materials_rect_size / 2}, materials_rect_size};
    int materials_drawn_pixels = 2 * materials_rect.size.x + 2 * materials_rect.size.y - 4;
    int materials_boundary = materials_drawn_pixels * this->materials.GetDirt() / this->materials.GetDirtCapacity();
    ShapeRenderer::DrawRectanglePart(surface, materials_rect, 0, materials_boundary,
                                     Palette.Get(Colors::DirtShieldActive));
    ShapeRenderer::DrawRectanglePart(surface, materials_rect, materials_boundary, materials_drawn_pixels,
                                     Palette.Get(Colors::DirtShieldPassive));

    /* Dirt layer */
    Size energy_rect_size = BaseSize + Size{4, 4};
    Rect energy_rect = Rect{Position{this->position - energy_rect_size / 2}, energy_rect_size};
    int energy_drawn_pixels = 2 * energy_rect.size.x + 2 * energy_rect.size.y - 4;
    int energy_boundary = energy_drawn_pixels * this->reactor.GetEnergy() / this->reactor.GetEnergyCapacity();
    ShapeRenderer::DrawRectanglePart(surface, energy_rect, 0, energy_boundary, Palette.Get(Colors::EnergyShieldActive));
    ShapeRenderer::DrawRectanglePart(surface, energy_rect, energy_boundary, energy_drawn_pixels,
                                     Palette.Get(Colors::EnergyShieldPassive));
}

void TankBase::Advance() { this->reactor.Add(tweak::base::ReactorRecoveryRate); }
