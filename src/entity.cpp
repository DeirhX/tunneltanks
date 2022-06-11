#include "pch.h"
#include "entity.h"

#include "tweak.h"

namespace crust
{

EntitySystem::EntitySystem()
{
    ecs::registry_filler(registry).feature<struct collisions>(
        ecs::feature().add_system<systems::UpdateSectorPositions>().add_system<systems::CollisionSystem>());
}

void EntitySystem::Advance() { registry.process_event(detect_collisions_step()); }

void systems::CollisionSystem::process(ecs::registry & owner, const detect_collisions_step & evt) {}

void systems::UpdateSectorPositions::process(ecs::registry & owner, const detect_collisions_step & evt) {}
} // namespace crust
