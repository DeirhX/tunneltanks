#pragma once

#include <drawbuffer.h>
#include <types.h>
#include "tweak.h"
#include <memory>
#include <array>

enum class BaseCollision
{
	None,
	Yours,
	Enemy,
};

enum class LevelVoxel : char
{
	Blank = ' ',
	DirtHigh = 'D',
	DirtLow = 'd',
	DirtGrow = 'g',
	Rock = 'r',
	Decal = '.',
	BaseMin = '0', // goes up to '7' for various tank colors
	BaseMax = '7',

	LevelGenDirt = 0,
	LevelGenRock = 1,
	LevelGenMark = 2,
};
class Voxels
{
public:
	static bool IsDirt(LevelVoxel voxel) { return voxel == LevelVoxel::DirtHigh || voxel == LevelVoxel::DirtLow; }
	static bool IsCollider(LevelVoxel voxel) { return voxel == LevelVoxel::Rock || (voxel >= LevelVoxel::BaseMin && voxel <= LevelVoxel::BaseMax); }
	static bool IsBase(LevelVoxel voxel) { return (voxel >= LevelVoxel::BaseMin && voxel <= LevelVoxel::BaseMax); }
};


class Level
{
public:
	
private:
	std::unique_ptr<LevelVoxel> array;
	Size size;
	DrawBuffer* drawBuffer;
	std::array<Position, MAX_TANKS> spawn;

public:
	Level(Size size, DrawBuffer* db);

	Size GetSize() const { return size; };

	void SetVoxel(Position pos, LevelVoxel voxel);
	LevelVoxel GetVoxel(Position pos) const;
	LevelVoxel& Voxel(Position pos);

	void SetVoxelRaw(Position pos, LevelVoxel voxel);
	void SetVoxelRaw(int offset, LevelVoxel voxel);
	LevelVoxel GetVoxelRaw(Position pos) const;
	LevelVoxel GetVoxelRaw(int offset) const;
	LevelVoxel& VoxelRaw(Position pos);

	int CountNeighborValues(Position pos);
	int CountNeighbors(Position pos, LevelVoxel neighbor_value);
   template <typename CountFunc>
	int CountNeighbors(Position pos, CountFunc count_func);

	// Level generate
	void GenerateDirtAndRocks();
	void CreateBases();
	 template <typename VoxelFunc>
	void ForEachVoxel(VoxelFunc func);

	Position GetSpawn(TankColor color) const;
	void SetSpawn(TankColor color, Position pos);
	bool DigHole(Position pos);
	BaseCollision CheckBaseCollision(Position pos, TankColor color);

	void CommitPixel(Position pos) const;
	void CommitAll() const;
	void DumpBitmap(const char* filename) const;

	static Color GetVoxelColor(LevelVoxel voxel);
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
