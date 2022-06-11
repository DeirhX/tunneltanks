#pragma once
#include "containers.h"
#include <ecs.hpp/ecs.hpp>

#include "tweak.h"
namespace ecs = ecs_hpp;

namespace crust
{

class EntitySystem
{
public:
    ecs::registry registry;
    const ecs::registry & const_registry = registry;

public:
    EntitySystem();
    void Advance();
};

inline EntitySystem entities{};


struct detect_collisions_step {};
struct fixed_simulation_step
{
    static constexpr std::chrono::microseconds dt = tweak::world::AdvanceStep;
};

namespace systems
{
    class CollisionSystem : public ecs::system<detect_collisions_step>
    {
      public:
        void process(ecs::registry & owner, const detect_collisions_step & evt) override;
    };

    class UpdateSectorPositions : public ecs::system<detect_collisions_step>
    {
      public:
        void process(ecs::registry & owner, const detect_collisions_step & evt) override;  
    };

} // namespace systems





namespace components
{
    struct TimeToLiveComponent { float life_left;};
} // namespace components

namespace aspect
{
    using namespace components;
  // ecs::aspect<PositionComponent, VelocityComponent> Movable;
}


} // namespace crust