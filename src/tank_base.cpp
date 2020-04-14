#include "tank_base.h"
#include "world.h"

TankBase::TankBase(Position position)
    : position(position)
{
    
}

void TankBase::RegisterLinkPoint(World * world)
{
    assert(!this->link_point);
    this->link_point = world->GetLinkMap()->RegisterLinkPoint(LinkPoint{this->position});
}

void TankBase::AbsorbResources(Resources & other)
{ this->resources.Absorb(other); }
