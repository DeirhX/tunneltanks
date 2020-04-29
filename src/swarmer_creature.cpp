﻿#include "swarmer_creature.h"


#include "shape_renderer.h"
#include "world.h"

Swarmer::Swarmer(Position position_, Level * level)
    : Base(position_, tweak::tank::DefaultTankReactor, tweak::tank::ResourcesMax, level)
{
    
}

CollisionType Swarmer::TryCollide(Direction at_rotation, Position at_position)
{
    CollisionType result = CollisionType::None;

    ShapeInspector::InspectRectangle(
        this->bounding_box.GetRect(at_position),
        [this, &result](Position position_) {
            bool is_blocking_collision = GetWorld()->GetCollisionSolver()->TestCollide(
                position_,
                [this, &result](Tank & tank) {
                    result = CollisionType::Blocked;
                    return true;
                },
                [&result](auto & machine) {
                    /* Collisions with machines disabled */
                    if (machine.IsBlockingCollision())
                    {
                        return true;
                    }
                    return false;
                },
                [&result](LevelPixel & pixel) {
                    if (Pixel::IsDirt(pixel))
                        result = CollisionType::Dirt;

                    if (Pixel::IsBlockingCollision(pixel))
                    {
                        result = CollisionType::Blocked;
                        return true;
                    }
                    return false;
                });

            return !is_blocking_collision;
        });
    return result;
}

void Swarmer::ApplyControllerOutput(ControllerOutput controls)
{

}

void Swarmer::Die()
{
    GetWorld()->GetProjectileList()->Add(
        ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                     tweak::explosion::death::Speed, tweak::explosion::death::Frames)
            .Explode<Shrapnel>(this->level));
}
