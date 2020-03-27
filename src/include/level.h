#pragma once

#include "tweak.h"
#include <array>
#include <drawbuffer.h>
#include <memory>
#include <types.h>
#include <vector>

#include "bitmaps.h"
#include "parallelism.h"

enum class BaseCollision
{
    None,
    Yours,
    Enemy,
};

/*
 * LevelPixel
 * Core pixel state of the level. Terrain, bases, rock and every other pixel kind there is.
 */
enum class LevelPixel : char
{
    Blank = ' ',     /* Nothing. The void of the space. */
    DirtHigh = 'D',  /* Standard dirt */
    DirtLow = 'd',   /* Standard dirt */
    DirtGrow = 'g',  /* Regrowing dirt, not yet collidable */
    Rock = 'r',      /* Indestructible (almost) */
    DecalHigh = '.', /* Decal after explosion. Harder to regrow */
    DecalLow = ',',  /* Decal after explosion. Harder to regrow */
    BaseMin = '0',   /* Tank Base. Goes up to '7' for various tank colors */
    BaseMax = '7',
    ConcreteLow = 'c',  /* Hardened concrete, tough to destroy */
    ConcreteHigh = 'C', /* Hardened concrete, tough to destroy */

    LevelGenDirt = 0,
    LevelGenRock = 1,
    LevelGenMark = 2,
};
/*
 * Queries that can be made against the pixel to classify it into various groups 
 */
class Pixel
{
  public:
    static bool IsDirt(LevelPixel voxel) { return voxel == LevelPixel::DirtHigh || voxel == LevelPixel::DirtLow; }
    static bool IsDiggable(LevelPixel voxel)
    {
        return voxel == LevelPixel::DirtHigh || voxel == LevelPixel::DirtLow || voxel == LevelPixel::DirtGrow;
    }
    static bool IsSoftCollision(LevelPixel voxel) { return IsDirt(voxel); }
    static bool IsBlockingCollision(LevelPixel voxel)
    {
        return voxel == LevelPixel::Rock || IsConcrete(voxel) ||
               (voxel >= LevelPixel::BaseMin && voxel <= LevelPixel::BaseMax);
    }
    static bool IsAnyCollision(LevelPixel voxel) { return IsSoftCollision(voxel) || IsBlockingCollision(voxel); }
    static bool IsBase(LevelPixel voxel) { return (voxel >= LevelPixel::BaseMin && voxel <= LevelPixel::BaseMax); }
    static bool IsScorched(LevelPixel voxel) { return voxel == LevelPixel::DecalHigh || voxel == LevelPixel::DecalLow; }
    static bool IsConcrete(LevelPixel voxel)
    {
        return voxel == LevelPixel::ConcreteHigh || voxel == LevelPixel::ConcreteLow;
    }
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

/*
 * Container for raw level data
 */
class LevelData
{
    using Container = ValueArray<LevelPixel>;
    Container array;

  public:
    LevelData(Size size) : array(size) {}

    LevelPixel & operator[](int i) { return array[i]; }
    const LevelPixel & operator[](int i) const { return array[i]; }

    //LevelPixel GetLevelData(Position pos);
    //LevelPixel GetLevelData(int offset) { return operator[](offset); }

    Container::iterator begin() { return array.begin(); }
    Container::iterator end() { return array.end(); }
    Container::const_iterator cbegin() const { return array.cbegin(); }
    Container::const_iterator cend() const { return array.cend(); }
};

/*
 * Adjacency Data
 */

class LevelAdjacencyDataCompressed
{
    /* Max adjacent pixels = 8. We need 3 bits for that. We'll give 4.*/
    using Container = std::vector<uint8_t>;
    Container array;
    constexpr static uint8_t Invalid = 0xF;

  public:
    LevelAdjacencyDataCompressed(Size size) { array.resize(size.x * size.y / 2); }

    uint8_t operator[](int i) const;
    void Set(int i, uint8_t value);
    //uint8_t & operator[](int i)
    //{
    //    return const_cast<uint8_t &>(const_cast<const LevelAdjacencyData *>(this)->operator[](i));
    //}
};

class Level
{
  private:
    LevelData data;
    Size size;
    LevelDrawBuffer * drawBuffer;
    std::vector<std::unique_ptr<TankBase>> spawn;

  private:
    void SetLevelData(int i, LevelPixel value) { this->data[i] = value; }
    void SetLevelData(Position pos, LevelPixel value);

  public:
    Level(Size size, LevelDrawBuffer * db);

    Size GetSize() const { return size; };

    /* Voxel get-set-reference operations */
    void SetVoxel(Position pos, LevelPixel voxel);
    LevelPixel GetVoxel(Position pos) const;
    //LevelPixel & Voxel(Position pos);

    void SetVoxelRaw(Position pos, LevelPixel voxel);
    void SetVoxelRaw(int offset, LevelPixel voxel) { SetLevelData(offset, voxel); }
    LevelPixel GetVoxelRaw(Position pos) const;
    LevelPixel GetVoxelRaw(int offset) const { return this->data[offset]; }
    //LevelPixel & VoxelRaw(Position pos);

    /* Draw buffer interaction */
    void CommitPixel(Position pos) const;
    void CommitAll() const;
    void DumpBitmap(const char * filename) const;

    /* Color lookup. Can be somewhere else. */
    static Color32 GetVoxelColor(LevelPixel voxel);

    /* Count neighbors is used when level building and for ad-hoc queries (e.g. dirt regeneration) */
    int CountNeighborValues(Position pos);
    int CountNeighbors(Position pos, LevelPixel neighbor_value);
    template <typename CountFunc>
    int CountNeighborValues(Position pos, CountFunc count_func);

    /* Level generation */
    void GenerateDirtAndRocks();
    void CreateBases();
    template <typename VoxelFunc>
    void ForEachVoxel(VoxelFunc func);
    template <typename VoxelFunc>
    void ForEachVoxelParallel(VoxelFunc func, WorkerCount worker_count = {});

    /* Tank-related stuff */
    TankBase * GetSpawn(TankColor color) const;
    void SetSpawn(TankColor color, std::unique_ptr<TankBase> && tank_base);
    void SetSpawn(TankColor color, Position position);
    bool DigHole(Position pos);
    BaseCollision CheckBaseCollision(Position pos, TankColor color);

  private:
    bool IsInBounds(Position pos) const;
    void CreateBase(Position pos, TankColor color);
};

class SafePixelAccessor
{
    Level * level;
    Position position;
    int index;
  public:
    SafePixelAccessor(Level * level, Position pos, Size size) : level(level), position(pos), index(pos.x + pos.y*size.x) {}
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
                voxelFunc(SafePixelAccessor(this, pos, this->GetSize()), threadLocal);
            }
        return 0;
    };

    parallel_for(parallel_slice, 0, this->GetSize().x - 1, worker_count);
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
