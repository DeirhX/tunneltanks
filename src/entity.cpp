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

}

void systems::UpdateSectorPositions::process(ecs::registry & owner, const detect_collisions_step & evt)
{
    owner.for_joined_components<PositionF, BoundingBoxF, components::Sector>(
        [&evt](ecs::entity, PositionF & pos, const BoundingBoxF & bbox, components::Sector & sector)
        {
            auto sectorId = GetWorld()->GetSectors().SectorIdForPosition(pos);
            (void)sectorId;
        });

    owner.for_joined_components<Position, BoundingBox, components::Sector>(
        [&evt](ecs::entity, Position & pos, const BoundingBox & bbox, components::Sector & sector)
        {
            //auto bounding_rect = Rect bbox.GetRect(pos);
            //auto topLeftId = GetWorld()->GetSectors().SectorIdForPosition({bounding_rect.Left(), bounding_rect.Top()});
            //(void)sectorId;
        });

    // Does not have a bounding box, point-only check is easier
    owner.for_joined_components<Position, components::Sector>(
        [&evt](ecs::entity, Position & pos, components::Sector & sector)
        {
            auto sectorId = GetWorld()->GetSectors().SectorIdForPosition(PositionF(pos));
            if (sector.sector_ids.size() != 1 || sector.sector_ids[0] != sectorId)
                sector.sector_ids = decltype(sector.sector_ids){sectorId};
            
        },!ecs::exists<BoundingBox>{});
    
}
} // namespace crust
