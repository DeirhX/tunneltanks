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

bool Controllable::HandleMove(DirectionF torch_heading, bool torch_use)
{
    /* Calculate the direction: */
    if (this->speed.x == 0 && this->speed.y == 0)
        return false;
    
    Direction dir = Direction::FromSpeed(this->speed);
    CollisionType collision = this->TryCollide(dir, this->position + 1 * this->speed);
    /* Now, is there room to move forward in that direction? */
    if (collision != CollisionType::None)
    {
        /* Attempt to dig and see the results */
        DigResult dug = this->level->DigTankTunnel(this->position + (1 * this->speed), torch_use);
        this->resources.Add({dug.dirt, dug.minerals});

        /* If we didn't use a torch pointing roughly in the right way, we don't move in the frame of digging*/
        if (!(torch_use &&
              Direction::FromSpeed(Speed{int(std::round(torch_heading.x)), int(std::round(torch_heading.y))}) == dir))
        {
            return false;
        }

        /* Now if we used a torch, test the collision again - we might have failed to dig some of the minerals */
        collision = this->TryCollide(dir, this->position + 1 * this->speed);
        if (collision != CollisionType::None)
            return false;
    }

    /* We're free to move, do it*/
    this->direction = Direction{dir};
    this->position.x += this->speed.x;
    this->position.y += this->speed.y;
    return true;
}