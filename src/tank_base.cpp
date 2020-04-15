#include "tank_base.h"
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
void TankBase::AbsorbResources(Resources & other)
{
    this->resources.Absorb(other);
}

void TankBase::AbsorbResources(Resources & other, Cost rate)
{
    /* Absorb a maximum of rate limit from source */
    Resources absorber = {ResourceCapacity{rate}};
    absorber.Absorb(other);
    /* Give it over to us, possibly keeping any left-over remainder */
    this->resources.Absorb(absorber);
    /* Return the left-over to original source*/
    other.Absorb(absorber);
}
