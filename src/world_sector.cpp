#include "pch.h"
#include "world_sector.h"

void WorldSector::AddEntity(ecs::entity entity)
{
    assert(ranges::find(entities, entity.id()) == entities.end());
    entities.emplace_back(entity.id());
}

void WorldSector::RemoveEntity(ecs::entity entity)
{
    const auto it = ranges::find(entities, entity.id());
    assert(it != entities.end());
    entities.erase(it);
}
