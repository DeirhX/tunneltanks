#pragma once
#include "containers.h"
#include <ecs.hpp/ecs.hpp>
namespace ecs = ecs_hpp;

namespace crust
{

class EntitySystem
{
    ecs_hpp::registry registry;
};

namespace components
{
    struct TimeToLiveComponent { float life_left;};
} // namespace components

namespace aspect
{
    using namespace components;
    //ecs::aspect<PositionComponent, VelocityComponent> Movable;
}


} // namespace crust