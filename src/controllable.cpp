#include "controllable.h"
#include "world.h"

Controllable::Controllable(Position position_, const Reactor & starting_reactor_state,
                           MaterialCapacity material_capacity, Level * level_)
    : position(position_),
      link_source(GetWorld(), position, LinkPointType::Controllable), reactor(starting_reactor_state),
      resources(material_capacity), level(level_)
{
}

bool Controllable::HealthOrEnergyEmpty() const
{
    return this->reactor.GetHealth() <= 0 || this->reactor.GetEnergy() <= 0;
}
