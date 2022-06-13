#pragma once

#include "entity.h"
#include "vector_2d.h"
#include <boost/container/small_vector.hpp>
namespace crust
{

/*
 * Sector object containing ids of entities inside it
 */
class WorldSector
{
  public:
    using id_t = int;
    constexpr static size_t extent = 64;
    constexpr static size_t extentX = extent;
    constexpr static size_t extentY = extent;

    WorldSector(id_t id) : id(id) {}

  public:
    id_t id;

  private:
    boost::container::small_vector<ecs::entity_id, 64 / sizeof(ecs::entity_id)> entities;

  public:
    void AddEntity(ecs::entity_id entity);
    void RemoveEntity(ecs::entity_id entity);
};

/*
 * Container of all sector objects
 */
class WorldSectors
{
  private:
    SizeF worldSize;
    crust::vector_2d<WorldSector> sectors;

  public:
    WorldSectors(SizeF worldSize)
        : worldSize(worldSize), sectors(SectorCountFromWorldSize(worldSize),
                                        []()
                                        {
                                            static WorldSector::id_t id = 0;
                                            return id++;
                                        })
    {
    }

    [[nodiscard]] SizeF GetWorldSize() const { return worldSize; }

    constexpr WorldSector::id_t SectorIdForPosition(PositionF position) const
    {
        auto offset = IndexFromPosition(position);
        auto id = WorldSector::id_t(offset.x + offset.y * sectors.size().x);
        assert(worldSize.FitsInside(OffsetF(position)));
        assert(id >= 0 && sectors.at(id).id == id);
        return id;
    }
    constexpr WorldSector & SectorForPosition(PositionF position) { return GetAt(IndexFromPosition(position)); }
    constexpr Offset IndexFromPosition(PositionF position) const
    {
        return Offset(static_cast<size_t>(std::ceil(position.x)) / WorldSector::extentX,
                      static_cast<size_t>(std::ceil(position.y)) / WorldSector::extentY);
    }

    constexpr WorldSector & Get(WorldSector::id_t id)
    {
        assert(((void)sectors.at(id), true));
        return sectors[id];
    }
    constexpr const WorldSector & Get(WorldSector::id_t id) const
    {
        assert(((void)sectors.at(id), true));
        return sectors[id];
    }

  private:
    constexpr static Size SectorCountFromWorldSize(SizeF worldSize)
    {
        return {static_cast<int>(int(worldSize.x + WorldSector::extentX - 1) / WorldSector::extentX),
                static_cast<int>(int(worldSize.y + WorldSector::extentY - 1) / WorldSector::extentY)};
    }
    constexpr WorldSector & GetAt(Offset offset) { return sectors.get(offset); }
    constexpr const WorldSector & GetAt(Offset offset) const { return sectors.get(offset); }
};

namespace components
{
    // Sector component holds the list of sectors currently occupied by an entity
    class OccupiedSector
    {
      public:
        using sector_list_t = boost::container::small_vector<WorldSector::id_t, 4>;

      private:
        // Indicates presence in sector(s) - more than one is possible for anything that has an area
        sector_list_t sectorIds;

      public:
        void EnterSector(ecs::entity_id entity_id, WorldSector::id_t sectorId);
        void ExitSector(ecs::entity_id entity_id, WorldSector::id_t sectorId);
        void MoveToSectors(ecs::entity_id entity_id, sector_list_t incomingList);

      private:
        static void OnEnterNotify(ecs::entity_id entityId, WorldSector::id_t sectorId);
        static void OnExitNotify(ecs::entity_id entity_id, WorldSector::id_t sectorId);
    };
} // namespace components

} // namespace crust