#pragma once

#include <drawbuffer.h>
#include <types.h>
#include "tweak.h"
#include <memory>
#include <array>
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
	Blank = ' ',
	DirtHigh = 'D',
	DirtLow = 'd',
	DirtGrow = 'g',
	Rock = 'r',
	DecalHigh = '.',
	DecalLow = ',',
	BaseMin = '0', // goes up to '7' for various tank colors
	BaseMax = '7',

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
	static bool IsDiggable(LevelPixel voxel) { return voxel == LevelPixel::DirtHigh || voxel == LevelPixel::DirtLow || voxel == LevelPixel::DirtGrow; }
	static bool IsSoftCollision(LevelPixel voxel) { return IsDirt(voxel); }
	static bool IsBlockingCollision(LevelPixel voxel) { return voxel == LevelPixel::Rock || (voxel >= LevelPixel::BaseMin && voxel <= LevelPixel::BaseMax); }
	static bool IsAnyCollision(LevelPixel voxel) { return IsSoftCollision(voxel) || IsBlockingCollision(voxel); }
	static bool IsBase(LevelPixel voxel) { return (voxel >= LevelPixel::BaseMin && voxel <= LevelPixel::BaseMax); }
	static bool IsScorched(LevelPixel voxel) { return voxel == LevelPixel::DecalHigh || voxel == LevelPixel::DecalLow; }
};

/*
 * Tank Base, part of the level
 */
class TankBase
{
	Position position;
public:
	TankBase(Position position) : position(position) { }
	Position GetPosition() { return this->position; }
};



/*
 * Container for raw level data
 */
class LevelData
{
	using Container = ValueArray<LevelPixel>;
public:
	Container array;

	LevelData(Size size);
	
	LevelPixel& operator[](int i) { return array[i]; }
	const LevelPixel& operator[](int i) const { return array[i]; }

	Container::iterator begin() { return array.begin(); }
	Container::iterator end() { return array.end(); }
	Container::const_iterator cbegin() const { return array.cbegin(); }
	Container::const_iterator cend() const { return array.cend(); }
};

class Level
{
private:
	LevelData data;
	Size size;
	LevelDrawBuffer* drawBuffer;
	std::vector<std::unique_ptr<TankBase>> spawn;

public:
	Level(Size size, LevelDrawBuffer* db);

	Size GetSize() const { return size; };

	/* Voxel get-set-reference operations */
	void SetVoxel(Position pos, LevelPixel voxel);
	LevelPixel GetVoxel(Position pos) const;
	LevelPixel& Voxel(Position pos);

	void SetVoxelRaw(Position pos, LevelPixel voxel);
	void SetVoxelRaw(int offset, LevelPixel voxel);
	LevelPixel GetVoxelRaw(Position pos) const;
	LevelPixel GetVoxelRaw(int offset) const;
	LevelPixel& VoxelRaw(Position pos);

	/* Draw buffer interaction */
	void CommitPixel(Position pos) const;
	void CommitAll() const;
	void DumpBitmap(const char* filename) const;

	/* Color lookup. Can be somewhere else. */
	static Color32 GetVoxelColor(LevelPixel voxel);
	
	/* Count neighbors is used when level building and for ad-hoc queries (e.g. dirt regeneration) */
	int CountNeighborValues(Position pos);
	int CountNeighbors(Position pos, LevelPixel neighbor_value);
     template <typename CountFunc>
	int CountNeighbors(Position pos, CountFunc count_func);

	/* Level generation */
	void GenerateDirtAndRocks();
	void CreateBases();
	 template <typename VoxelFunc>
	void ForEachVoxel(VoxelFunc func);
	 template <typename VoxelFunc>
	void ForEachVoxelParallel(VoxelFunc func, WorkerCount worker_count = {});

	/* Tank-related stuff */
	TankBase* GetSpawn(TankColor color) const;
	void SetSpawn(TankColor color, std::unique_ptr<TankBase>&& tank_base);
	void SetSpawn(TankColor color, Position position); 
	bool DigHole(Position pos);
	BaseCollision CheckBaseCollision(Position pos, TankColor color);
private:
	bool IsInBounds(Position pos) const;
	
	void CreateBase(Position pos, TankColor color);
};

template <typename VoxelFunc> // requires{ voxelFunc(Position, LevelVoxel&) -> void; }
void Level::ForEachVoxel(VoxelFunc voxelFunc) 
{
	Position pos;
	for (pos.x = 0; pos.x < this->GetSize().x; ++pos.x)
		for (pos.y = 0; pos.y < this->GetSize().y; ++pos.y)
		{
			voxelFunc(pos, this->Voxel(pos));
		}
}
template <typename VoxelFunc> 
void Level::ForEachVoxelParallel(VoxelFunc voxelFunc, WorkerCount worker_count)
{
	auto parallel_slice = [this, voxelFunc](int min, int max, ThreadLocal* threadLocal)
	{
		Position pos;
		for (pos.x = min; pos.x <= max; ++pos.x)
			for (pos.y = 0; pos.y < this->GetSize().y; ++pos.y)
			{
				voxelFunc(pos, this->Voxel(pos), threadLocal);
			}
		return 0;
	};

	parallel_for(parallel_slice, 0, this->GetSize().x - 1, worker_count);
}

template <typename CountFunc>
int Level::CountNeighbors(Position pos, CountFunc count_func)
{
	return count_func(GetVoxelRaw({ pos.x - 1 + GetSize().x * (pos.y - 1) })) +
		count_func(GetVoxelRaw({ pos.x + GetSize().x * (pos.y - 1) })) +
		count_func(GetVoxelRaw({ pos.x + 1 + GetSize().x * (pos.y - 1) })) +
		count_func(GetVoxelRaw({ pos.x - 1 + GetSize().x * (pos.y) })) +
		count_func(GetVoxelRaw({ pos.x + 1 + GetSize().x * (pos.y) })) +
		count_func(GetVoxelRaw({ pos.x - 1 + GetSize().x * (pos.y + 1) })) +
		count_func(GetVoxelRaw({ pos.x + GetSize().x * (pos.y + 1) })) +
		count_func(GetVoxelRaw({ pos.x + 1 + GetSize().x * (pos.y + 1) }));
}
