#include "pch.h"
#include "entity.h"
#include "world.h"
#include "tweak.h"

namespace crust
{

EntitySystem::EntitySystem()
{
    ecs::registry_filler(registry).feature<struct collisions>(
        ecs::feature().add_system<systems::UpdateSectorPositions>().add_system<systems::CollisionSystem>());
}

void EntitySystem::Advance() { registry.process_event(detect_collisions_step()); }

void systems::CollisionSystem::process(ecs::registry & owner, const detect_collisions_step & evt)
{
    owner.for_joined_components<PositionF, BoundingBoxF>(
        [&evt](ecs::entity, PositionF & pos, const BoundingBoxF & bbox)
        { auto sectorId = GetWorld()->GetSectors().SectorIdForPosition(pos);
            (void)sectorId;
        });

    owner.for_joined_components<Position, BoundingBox>(
        [&evt](ecs::entity, Position & pos, const BoundingBox & bbox)
        {
            auto sectorId = GetWorld()->GetSectors().SectorIdForPosition(PositionF(pos));
            (void)sectorId;
        });
}

void systems::UpdateSectorPositions::process(ecs::registry & owner, const detect_collisions_step & evt) {}
} // namespace crust
