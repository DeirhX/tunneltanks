#include "tank_base.h"

#include "shape_renderer.h"
#include "world.h"

TankBase::TankBase(Position position, TankColor color) : position(position), color(color) {
    
}

void TankBase::RegisterLinkPoint(World * world)
{
    assert(!this->link_point);
    this->link_point = world->GetLinkMap()->RegisterLinkPoint(LinkPoint{this->position});
}

bool TankBase::IsInside(Position tested_position) const
{
    return this->bounding_box.IsInside(tested_position, this->position);
}
void TankBase::AbsorbResources(MaterialContainer & other) {
    this->resources.Absorb(other);
}

void TankBase::AbsorbResources(MaterialContainer & other, MaterialAmount rate)
{
    /* Absorb a maximum of rate limit from source */
    MaterialContainer absorber = {MaterialCapacity{rate}};
    absorber.Absorb(other);
    /* Give it over to us, possibly keeping any left-over remainder */
    this->resources.Absorb(absorber);
    /* Return the left-over to original source*/
    other.Absorb(absorber);
}

void TankBase::Draw(Surface * surface) const
{
    /* Energy layer */
    Size energy_rect_size = BaseSize + Size{2, 2};
    Rect energy_rect = Rect{Position{this->position - energy_rect_size / 2}, energy_rect_size};
    int energy_drawn_pixels = 2 * energy_rect.size.x + 2 * energy_rect.size.y - 4;
    ShapeRenderer::DrawRectanglePart(surface, energy_rect, 0, energy_drawn_pixels / 3,
                                 Palette.Get(Colors::EnergyFieldMedium));
    ShapeRenderer::DrawRectanglePart(surface, energy_rect, energy_drawn_pixels / 3, energy_drawn_pixels,
                                     Palette.Get(Colors::ConcreteLow));

    /* Dirt layer */
    Size dirt_rect_size = BaseSize + Size{4, 4};
    Rect dirt_rect = Rect{Position{this->position - dirt_rect_size / 2}, dirt_rect_size}; 
    int dirt_drawn_pixels = 2 * energy_rect.size.x + 2 * energy_rect.size.y - 4;
    ShapeRenderer::DrawRectanglePart(surface, dirt_rect, 0, dirt_drawn_pixels / 4,
                                     Palette.Get(Colors::EnergyFieldHigh));
    ShapeRenderer::DrawRectanglePart(surface, dirt_rect, dirt_drawn_pixels / 4, dirt_drawn_pixels,
                                     Palette.Get(Colors::ConcreteHigh));
}

void TankBase::Advance()
{ this->reactor.Add(tweak::base::ReactorRecoveryRate); }
