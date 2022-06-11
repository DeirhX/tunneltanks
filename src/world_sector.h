#pragma once

#include "entity.h"
#include "vector_2d.h"
#include <boost/container/small_vector.hpp>

class WorldSector
{
  public:
    using id_t = int;
    constexpr static size_t extent = 64;
    constexpr static size_t extentX = extent;
    constexpr static size_t extentY = extent;

    WorldSector(id_t id) : id(id) {}

  private:
    id_t id;
    boost::container::small_vector<ecs::entity_id, 64 / sizeof(ecs::entity_id)> entities;

  public:
    void AddEntity(ecs::entity entity);
    void RemoveEntity(ecs::entity entity);
};

class WorldSectors
{
  private:
    Size worldSize;
    crust::vector_2d<WorldSector> sectors;

  public:
    WorldSectors(Size worldSize)
        : worldSize(worldSize), sectors(SectorCountFromWorldSize(worldSize),[]()
                                        {
                                            static WorldSector::id_t id = 0;
                                            return ++id;
                                        })
    {
    }

    [[nodiscard]] Size GetWorldSize() const { return worldSize; }
    WorldSector & GetAt(Offset offset) { return sectors.get(offset); }
    const WorldSector & GetAt(Offset offset) const { return sectors.get(offset); }

  private:
    constexpr static Size SectorCountFromWorldSize(Size worldSize)
    {
        return {static_cast<int>((worldSize.x + WorldSector::extentX - 1) / WorldSector::extentX),
                static_cast<int>((worldSize.y + WorldSector::extentY - 1) / WorldSector::extentY)};
    }

};

struct WorldSectorMember
{
    ecs::entity_id sectorId;
};