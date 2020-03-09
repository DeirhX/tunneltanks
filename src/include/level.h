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

using LevelVoxel = char;

class Level
{
public:
	
private:
	std::unique_ptr<LevelVoxel>  array;
	Size    size;
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

	// Level generate
	void CreateDirtAndRocks();
	void CreateBases();
	 template <typename VoxelFunc>
	void ForEachVoxel(VoxelFunc func);

	Position GetSpawn(TankColor color) const;
	void SetSpawn(TankColor color, Position pos);
	bool DigHole(Position pos);
	BaseCollision CheckBaseCollision(Position pos, TankColor color);

	void CommitPixel(Position pos) const;
	void CommitAll() const;
	void DumpBitmap(const char* filename);
private:
	bool IsInBounds(Position pos) const;
	
	void CreateBase(Position pos, TankColor color);
};

template <typename VoxelFunc>
void Level::ForEachVoxel(VoxelFunc voxelFunc)
{
	for (int x = 0; x < this->GetSize().x; ++x)
		for (int y = 0; y < this->GetSize().y; ++y)
		{
			voxelFunc(this->Voxel({ x, y }));
		}
}

///* (Con|De)structor: */
//Level *level_new(DrawBuffer *b, int w, int h) ;
//void   level_destroy(Level *lvl) ;
//
///* Exposes the level data: */
//void level_set(Level *lvl, int x, int y, char data) ;
//char level_get(Level *lvl, int x, int y) ;
//
///* Decorates a level for drawing. Should be called by level generators: */
//void level_decorate(Level *lvl) ;
//void level_make_bases(Level *lvl) ;
//
//Vector level_get_spawn(Level *lvl, int i);
//
//int level_dig_hole(Level *lvl, int x, int y) ;
//
//void level_draw_all(Level *lvl, DrawBuffer *b) ;
//void level_draw_pixel(Level *lvl, DrawBuffer *b, int x, int y) ;
//
///* Will return a value indicating coll: */
//
//
///* Dumps a decorated level into a color bmp file: */
//void level_dump_bmp(Level *lvl, const char *filename) ;
//
//
//
