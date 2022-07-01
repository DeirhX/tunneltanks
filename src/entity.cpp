#include "pch.h"
#include "entity.h"
#include "world.h"
#include "tweak.h"
#include <algorithm>

namespace crust
{
EntitySystem::EntitySystem()
{
    ecs::registry_filler(registry).feature<struct collisions>(
        ecs::feature().add_system<systems::UpdateSectorPositions>().add_system<systems::CollisionSystem>());
}

void EntitySystem::Begin() { registry.process_event(detect_collisions_step()); }

void EntitySystem::Advance() { registry.process_event(detect_collisions_step()); }

void EntitySystem::Clear()
{
    registry = std::move(ecs::registry());
}

void systems::CollisionSystem::process(ecs::registry & owner, const detect_collisions_step & evt)
{

}

void systems::UpdateSectorPositions::process(ecs::registry & owner, const detect_collisions_step & evt)
{
    owner.for_joined_components<PositionF, BoundingBoxF, components::OccupiedSector>(
        [&evt](ecs::entity entity, PositionF & pos, const BoundingBoxF & bbox, components::OccupiedSector & sector)
        {
            auto bounding_rect = bbox.GetRect(pos);
            boost::container::small_vector<WorldSector::id_t, 4> occupiedSectors;
            bounding_rect.VisitAllPoints(
                [&occupiedSectors](PositionF point)
                { occupiedSectors.emplace_back(GetWorld()->GetSectors().SectorIdForPosition(point)); });
            // Just overwrite it
            sector.MoveToSectors(entity, occupiedSectors);
        });

    // Has a bounding box we'll use for sector assignment.
    // Theoretical maximum: 4 sectors
    owner.for_joined_components<Position, BoundingBox, components::OccupiedSector>(
        [&evt](ecs::entity entity, Position & pos, const BoundingBox & bbox, components::OccupiedSector & sector)
        {
            auto bounding_rect = bbox.GetRect(pos);
            boost::container::small_vector<WorldSector::id_t, 4> occupiedSectors;
            bounding_rect.VisitAllPoints([&occupiedSectors](Position point)
                { occupiedSectors.emplace_back(GetWorld()->GetSectors().SectorIdForPosition(PositionF(point)));
            });
            // Just overwrite it
            sector.MoveToSectors(entity, occupiedSectors);
        });

    // Does not have a bounding box, point-only check is easier
    // Theoretical maximum: always 1 sector
    owner.for_joined_components<Position, components::OccupiedSector>(
        [&evt](ecs::entity entity, Position & pos, components::OccupiedSector & sector)
        {
            auto sectorId = GetWorld()->GetSectors().SectorIdForPosition(PositionF(pos));
            //if (sector.sector_ids.size() != 1 || sector.sector_ids[0] != sectorId)
            sector.MoveToSectors(entity,{sectorId});
            
        },!ecs::exists<BoundingBox>{});
    
}
} // namespace crust
