#include "pch.h"
#include "world_sector.h"
#include <algorithm>

#include "world.h"

namespace crust
{

/*
 * Add/Remove entity ids to to/from this sector
 */
void WorldSector::AddEntity(ecs::entity_id entityId)
{
    assert(ranges::find(entities, entityId) == entities.end());
    entities.emplace_back(entityId);
}

void WorldSector::RemoveEntity(ecs::entity_id entityId)
{
    const auto it = ranges::find(entities, entityId);
    assert(it != entities.end());
    entities.erase(it);
}

/*
 * Process change notifications of modifying the local list of occupied sectors for an entity
 */

void components::OccupiedSector::EnterSector(ecs::entity_id entityId, WorldSector::id_t sectorId)
{
    OnEnterNotify(entityId, sectorId);
    this->sectorIds.push_back(sectorId);
}

void components::OccupiedSector::ExitSector(ecs::entity_id entityId, WorldSector::id_t sectorId)
{
    OnExitNotify(entityId, sectorId);
    auto existing_it = ranges::find(sectorIds, sectorId);
    assert(existing_it != this->sectorIds.end());
    this->sectorIds.erase(existing_it);
}

void components::OccupiedSector::MoveToSectors(ecs::entity_id entity_id, sector_list_t incomingList)
{
    ranges::sort(incomingList);
    auto current_it = this->sectorIds.begin();
    auto incoming_it = incomingList.begin();
    // Iterate only unique elements
    auto incoming_end = ranges::unique(incomingList);
    incomingList.erase(incoming_end.begin(), incoming_end.end());

    /* Compare with incoming list and generate events on what needs to be changed */
    bool change_found = false;
    while (incoming_it != incomingList.end() || current_it != this->sectorIds.end())
    {
        if (incoming_it == incomingList.end() || (current_it != this->sectorIds.end() && *current_it < *incoming_it))
        {
            OnExitNotify(entity_id, *current_it);
            change_found = true;
            ++current_it;
        }
        else if (current_it == this->sectorIds.end() ||
                 (incoming_it != incomingList.end() && *current_it > *incoming_it))
        {
            OnEnterNotify(entity_id, *incoming_it);
            change_found = true;
            ++incoming_it;
        }
        else // *current_it == *incoming_it
        {
            if (incoming_it != incomingList.end())
                ++incoming_it;
            if (current_it != this->sectorIds.end())
                ++current_it;
        }
    }
    if (change_found)
        this->sectorIds = incomingList;
}

void components::OccupiedSector::OnEnterNotify(ecs::entity_id entityId, WorldSector::id_t sectorId)
{
    GetWorld()->GetSectors().Get(sectorId).AddEntity(entityId);
}

void components::OccupiedSector::OnExitNotify(ecs::entity_id entityId, WorldSector::id_t sectorId)
{
    GetWorld()->GetSectors().Get(sectorId).RemoveEntity(entityId);
}

} // namespace crust