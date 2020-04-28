#include "swarmer_creature.h"

#include "world.h"

Swarmer::Swarmer(Position position_, Level * level)
    : Base(position_, tweak::tank::DefaultTankReactor, tweak::tank::ResourcesMax, level)
{
    
}

CollisionType Swarmer::TryCollide(Direction at_rotation, Position at_position)
{
    if (GetWorld()->GetLevel()->IsInside(at_position))
        return CollisionType::None;
    return CollisionType::Blocked;
}

void Swarmer::ApplyControllerOutput(ControllerOutput controls)
{
}
