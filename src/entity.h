#pragma once
#include <ecs.hpp/ecs.hpp>

#include "containers.h"
#include "tweak.h"
#include <boost/container/small_vector.hpp>

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
    void Begin(); /* TODO: Remove once fully ecs */
    void Advance();
};

inline EntitySystem entities{};

struct detect_collisions_step
{
};
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
    struct TimeToLiveComponent
    {
        float life_left;
    };
} // namespace components

namespace aspect
{
    using namespace components;


    template <typename... Aspect>
    struct entity_aspects
    {
        static bool verify(ecs::entity entity) { return true; }
    };
    template <typename Aspect, typename... Aspects>
    struct entity_aspects<Aspect, Aspects...>
    {
        static bool verify(ecs::entity entity)
        {
            return Aspect::match_entity(entity) && entity_aspects<Aspects...>::verify(entity);
        }
    };


    // ecs::aspect<PositionComponent, VelocityComponent> Movable;
} // namespace aspect

}