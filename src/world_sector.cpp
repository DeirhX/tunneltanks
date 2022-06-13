#include "pch.h"
#include "world_sector.h"
#include <algorithm>

namespace crust
{

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

void components::OccupiedSector::EnterSector(WorldSector::id_t sectorId) {}

void components::OccupiedSector::ExitSector(WorldSector::id_t sectorId) {}

void components::OccupiedSector::MoveToSectors(sector_list_t incomingList)
{
    ranges::sort(incomingList);
    auto current_it = this->sector_ids.begin();
    auto incoming_it = incomingList.begin();
    // Iterate only unique elements
    auto incoming_end = ranges::unique(incomingList);
    incomingList.erase(incoming_end.begin(), incoming_end.end());

    /* Compare with incoming list and generate events on what needs to be changed */
    bool change_found = false;
    while (incoming_it != incomingList.end() || current_it != this->sector_ids.end())
    {
        if (incoming_it == incomingList.end() || (current_it != this->sector_ids.end() && *current_it < *incoming_it))
        {
            OnExitNotify(*current_it);
            change_found = true;
            ++current_it;
        }
        else if (current_it == this->sector_ids.end() ||
                 (incoming_it != incomingList.end() && *current_it > *incoming_it))
        {
            OnEnterNotify(*incoming_it);
            change_found = true;
            ++incoming_it;
        }
        else // *current_it == *incoming_it
        {
            if (incoming_it != incomingList.end())
                ++incoming_it;
            if (current_it != this->sector_ids.end())
                ++current_it;
        }
    }
    if (change_found)
        this->sector_ids = incomingList;
}

void components::OccupiedSector::OnEnterNotify(WorldSector::id_t sectorId) {}

void components::OccupiedSector::OnExitNotify(WorldSector::id_t sectorId) {}

} // namespace crust