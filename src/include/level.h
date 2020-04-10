#pragma once

#include "tweak.h"
#include <array>

#include <memory>
#include <types.h>
#include <vector>

#include "bitmaps.h"
#include "containers.h"
#include "level_adjacency.h"
#include "parallelism.h"
#include "render_surface.h"

enum class LevelPixel : char;

enum class BaseCollision
{
    None,
    Yours,
    Enemy,
};

struct DigResult
{
    int dirt = 0;
    int minerals = 0;
};

/*
 * Tank Base, part of the level
 */
class TankBase
{
    Position position;

  public:
    TankBase(Position position) : position(position) {}
    Position GetPosition() { return this->position; }
};


struct LevelSurfaces
{
    LevelSurfaces(Size size) : terrain_surface(size, false), objects_surface(size, true) {}
    /* Holds rendered texture of the terrain, materializing each LevelPixel into color */
    WorldRenderSurface terrain_surface; 
    /* Holds a layer of frequently changed objects that will be drawn on top of terrain*/
    WorldRenderSurface objects_surface; 
};

class Level
{
  private:
    Size size;
    Container2D<LevelPixel> data; /* Holds logical terrain pixels - enum LevelPixel : char */
    LevelSurfaces surfaces; /* Holds terrain and object surfaces for drawing */

    //DirtAdjacencyData dirt_adjacency_data;
    std::vector<std::unique_ptr<TankBase>> spawn;
    bool is_ready = false;

  private:
    void SetLevelData(int i, LevelPixel value);
    void SetLevelData(Position pos, LevelPixel value);

  public:
    Level(Size size);

    Size GetSize() const { return this->size; };
    LevelSurfaces * GetSurfaces() { return &this->surfaces; }

    /* Voxel get-set-reference operations */
    void SetPixel(Position pos, LevelPixel voxel);
    LevelPixel GetPixel(Position pos) const;
    //LevelPixel & Voxel(Position pos);

    void SetVoxelRaw(Position pos, LevelPixel voxel);
    void SetVoxelRaw(int offset, LevelPixel voxel) { SetLevelData(offset, voxel); }
    LevelPixel GetVoxelRaw(Position pos) const;
    LevelPixel GetVoxelRaw(int offset) const { return this->data[offset]; }
    //LevelPixel & VoxelRaw(Position pos);

    /* Terrain surface interaction */
    void CommitPixel(Position pos);
    void CommitPixels(const std::vector<Position>& positions);
    void CommitAll();
    void DumpBitmap(const char * filename) const;

    /* Color lookup. Can be somewhere else. */
    static Color GetVoxelColor(LevelPixel voxel);

    template<typename PixelCompareFunc>
    Position GetClosestPixel(Position origin, int max_radius, PixelCompareFunc compare_func);

    /* Count neighbors is used when level building and for ad-hoc queries (e.g. dirt regeneration) */
    int CountNeighborValues(Position pos);
    int CountNeighbors(Position pos, LevelPixel neighbor_value);
    template <typename CountFunc>
    int CountNeighborValues(Position pos, CountFunc count_func);
    //uint8_t DirtPixelsAdjacent(Position pos) { return this->dirt_adjacency_data.Get(pos); }

    void MaterializeLevelTerrainAndBases();

    template <typename VoxelFunc>
    void ForEachVoxel(VoxelFunc func);
    template <typename VoxelFunc>
    void ForEachVoxelParallel(VoxelFunc func, WorkerCount worker_count = {});

    /* Tank-related stuff */
    TankBase * GetSpawn(TankColor color) const;
    void SetSpawn(TankColor color, std::unique_ptr<TankBase> && tank_base);
    void SetSpawn(TankColor color, Position position);
    DigResult DigTankTunnel(Position pos, bool dig_with_torch);
    BaseCollision CheckBaseCollision(Position pos, TankColor color);

  private:
    /* Level generation */
    void GenerateDirtAndRocks();
    void CreateBases();

    bool IsInBounds(Position pos) const;
    void CreateBase(Position pos, TankColor color);
};

class SafePixelAccessor
{
    Level * level;
    Position position;
    int index;

  public:
    SafePixelAccessor(Level * level, Position pos, Size size)
        : level(level), position(pos), index(pos.x + pos.y * size.x)
    {
    }
    Position GetPosition() const { return this->position; }
    LevelPixel Get() const { return level->GetVoxelRaw(this->index); }
    void Set(LevelPixel pix) { level->SetVoxelRaw(this->index, pix); }
};

template <typename VoxelFunc> // requires{ voxelFunc(Position, SafePixelAccessor&) -> void; }
void Level::ForEachVoxel(VoxelFunc voxelFunc)
{
    Position pos;
    for (pos.x = 0; pos.x < this->GetSize().x; ++pos.x)
        for (pos.y = 0; pos.y < this->GetSize().y; ++pos.y)
        {
            voxelFunc(SafePixelAccessor(this, pos, this->GetSize()));
        }
}
template <typename VoxelFunc> // requires{ voxelFunc(Position, LevelVoxel&) -> void; }
void Level::ForEachVoxelParallel(VoxelFunc voxelFunc, WorkerCount worker_count)
{
    auto parallel_slice = [this, voxelFunc](int min, int max, ThreadLocal * threadLocal) {
        Position pos;
        for (pos.x = min; pos.x <= max; ++pos.x)
            for (pos.y = 0; pos.y < this->GetSize().y; ++pos.y)
            {
                voxelFunc(this->GetVoxelRaw(pos), SafePixelAccessor(this, pos, this->GetSize()), threadLocal);
            }
        return 0;
    };

    parallel_for(parallel_slice, 0, this->GetSize().x - 1, worker_count);
}

template <typename PixelCompareFunc>
Position Level::GetClosestPixel(Position origin, int max_radius, PixelCompareFunc compare_func)
{
    int square_radius = 1;
    float closest_distance = std::numeric_limits<float>::max();


}

template <typename CountFunc>
int Level::CountNeighborValues(Position pos, CountFunc count_func)
{
    return count_func(GetVoxelRaw({pos.x - 1 + GetSize().x * (pos.y - 1)})) +
           count_func(GetVoxelRaw({pos.x + GetSize().x * (pos.y - 1)})) +
           count_func(GetVoxelRaw({pos.x + 1 + GetSize().x * (pos.y - 1)})) +
           count_func(GetVoxelRaw({pos.x - 1 + GetSize().x * (pos.y)})) +
           count_func(GetVoxelRaw({pos.x + 1 + GetSize().x * (pos.y)})) +
           count_func(GetVoxelRaw({pos.x - 1 + GetSize().x * (pos.y + 1)})) +
           count_func(GetVoxelRaw({pos.x + GetSize().x * (pos.y + 1)})) +
           count_func(GetVoxelRaw({pos.x + 1 + GetSize().x * (pos.y + 1)}));
}
