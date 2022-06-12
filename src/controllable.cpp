#include "pch.h"
#include "controllable.h"
#include "world.h"
#include "position_component.h"
namespace crust
{

Controllable::Controllable(Position position_, const Reactor & starting_reactor_state,
                           MaterialCapacity material_capacity, Terrain * level_)
    : entity(crust::entities.registry.create_entity()), link_source(GetWorld(), position_, LinkPointType::Controllable),
      reactor(starting_reactor_state), resources(material_capacity), level(level_)
{
    entity.assign_component<Position>(position_);
    entity.assign_component<Speed>();
    entity.assign_component<DirectionF>();
}

bool Controllable::HealthOrEnergyEmpty() const
{
    return this->reactor.GetHealth() <= 0 || this->reactor.GetEnergy() <= 0;
}

bool Controllable::HandleMove(DirectionF torch_heading, bool torch_use)
{
    Position & position = this->PositionRef();
    Speed & speed = this->SpeedRef();

    /* Calculate the direction: */
    if (speed.x == 0 && speed.y == 0)
        return false;

    Direction dir = Direction::FromSpeed(speed);
    CollisionType collision = this->TryCollide(dir, position + 1 * speed);
    /* Now, is there room to move forward in that direction? */
    if (collision != CollisionType::None)
    {
        /* Attempt to dig and see the results */
        DigResult dug = this->level->DigTankTunnel(position + (1 * speed), torch_use);
        this->resources.Add({dug.dirt, dug.minerals});

        /* If we didn't use a torch pointing roughly in the right way, we don't move in the frame of digging*/
        if (!(torch_use &&
              Direction::FromSpeed(Speed{int(std::round(torch_heading.x)), int(std::round(torch_heading.y))}) == dir))
        {
            return false;
        }

        /* Now if we used a torch, test the collision again - we might have failed to dig some of the minerals */
        collision = this->TryCollide(dir, position + 1 * speed);
        if (collision != CollisionType::None)
            return false;
    }

    /* We're free to move, do it*/
    this->DirectionRef() = Direction{dir};
    position += speed;
    return true;
}
} // namespace crust