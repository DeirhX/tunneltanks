#pragma once

#include "containers.h"
#include "terrain_adjacency.h"
#include "parallelism.h"
#include "render_surface.h"
#include "tank_base.h"
#include "types.h"
#include <memory>
#include <vector>

class World;
enum class TerrainPixel : char;

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



class Terrain
{
  private:
    Size size;
    Container2D<TerrainPixel> data; /* Holds logical terrain pixels - enum TerrainPixel : char */

    std::vector<Position> change_list; /* List of changed pixels this frame */
    //DirtAdjacencyData dirt_adjacency_data;
    bool is_ready = false;

  private:
    void SetTerrainData(int i, TerrainPixel value);
    void SetTerrainData(Position pos, TerrainPixel value);

  public:
    Terrain(Size size);
    void OnConnectWorld(World * world);
    void BeginGame();

    [[nodiscard]] Size GetSize() const { return this->size; }
    [[nodiscard]] const Container2D<TerrainPixel> & GetLevelData() const { return this->data; }

    /* Voxel get-set-reference operations */
    void SetPixel(Position pos, TerrainPixel voxel);
    TerrainPixel GetPixel(Position pos) const;

    void SetVoxelRaw(Position pos, TerrainPixel voxel);
    void SetVoxelRaw(int offset, TerrainPixel voxel) { SetTerrainData(offset, voxel); }
    TerrainPixel GetVoxelRaw(Position pos) const;
    TerrainPixel GetVoxelRaw(int offset) const { return this->data[offset]; }

    /* Terrain surface interaction */
    void CommitPixel(Position pos);
    void CommitPixels(const std::vector<Position> & positions);
    void DrawChangesToSurface(WorldRenderSurface & world_surface);
    void DrawAllToSurface(WorldRenderSurface & world_surface);
    void DumpBitmap(const char * filename) const;

    void Advance() {};

    /* Color lookup. Can be somewhere else. */
    static Color GetVoxelColor(TerrainPixel voxel);

    /* Count neighbors is used when level building and for ad-hoc queries (e.g. dirt regeneration) */
    int CountNeighbors(Position pos, TerrainPixel neighbor_value);
    template <typename CountFunc>
    int CountNeighborValues(Position pos, CountFunc count_func);
    //uint8_t DirtPixelsAdjacent(Position pos) { return this->dirt_adjacency_data.Get(pos); }

    void MaterializeLevelTerrain();

    template <typename VoxelFunc>
    void ForEachVoxel(VoxelFunc func);
    template <typename VoxelFunc>
    void ForEachVoxelParallel(VoxelFunc func, WorkerCount worker_count = {});

    DigResult DigTankTunnel(Position pos, bool dig_with_torch);

    bool IsInside(Position position) const;
  private:
    /* Level generation */
    void GenerateDirtAndRocks();
};

class SafePixelAccessor
{
    Terrain * level;
    Position position;
    int index;

  public:
    SafePixelAccessor(Terrain * level, Position pos, Size size)
        : level(level), position(pos), index(pos.x + pos.y * size.x)
    {
    }
    Position GetPosition() const { return this->position; }
    TerrainPixel Get() const { return level->GetVoxelRaw(this->index); }
    void Set(TerrainPixel pix) { level->SetVoxelRaw(this->index, pix); }
};

template <typename VoxelFunc> // requires{ voxelFunc(Position, SafePixelAccessor&) -> void; }
void Terrain::ForEachVoxel(VoxelFunc voxelFunc)
{
    Position pos;
    for (pos.x = 0; pos.x < this->GetSize().x; ++pos.x)
        for (pos.y = 0; pos.y < this->GetSize().y; ++pos.y)
        {
            voxelFunc(SafePixelAccessor(this, pos, this->GetSize()));
        }
}
template <typename VoxelFunc> // requires{ voxelFunc(Position, LevelVoxel&) -> void; }
void Terrain::ForEachVoxelParallel(VoxelFunc voxelFunc, WorkerCount worker_count)
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

template <typename CountFunc>
int Terrain::CountNeighborValues(Position pos, CountFunc count_func)
{
    return count_func(GetVoxelRaw((pos.x - 1 + GetSize().x * (pos.y - 1)))) +
           count_func(GetVoxelRaw((pos.x + GetSize().x * (pos.y - 1)))) +
           count_func(GetVoxelRaw((pos.x + 1 + GetSize().x * (pos.y - 1)))) +
           count_func(GetVoxelRaw((pos.x - 1 + GetSize().x * (pos.y)))) +
           count_func(GetVoxelRaw((pos.x + 1 + GetSize().x * (pos.y)))) +
           count_func(GetVoxelRaw((pos.x - 1 + GetSize().x * (pos.y + 1)))) +
           count_func(GetVoxelRaw((pos.x + GetSize().x * (pos.y + 1)))) +
           count_func(GetVoxelRaw((pos.x + 1 + GetSize().x * (pos.y + 1))));
}
