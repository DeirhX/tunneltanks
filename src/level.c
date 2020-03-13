#include <cstdlib>

#include <level.h>
#include <memalloc.h>
#include <random.h>
#include <tweak.h>
#include <types.h>
#include <drawbuffer.h>
#include <gamelib.h>

#include "exceptions.h"
#include <cassert>

Level::Level(Size size, DrawBuffer* b)
	: size(size), drawBuffer (b)
{
	this->array.reset(new LevelVoxel[size.x * size.y]);
	for (int i = 0; i < size.x * size.y; ++i)
		this->array.get()[i] = LevelVoxel::LevelGenRock;
}

void Level::SetVoxel(Position pos, LevelVoxel voxel)
{
	if (!IsInBounds(pos))
		throw GameException("Invalid position");
	this->array.get()[ pos.y*this->size.x + pos.x ] = voxel;

	CommitPixel(pos);
}

void Level::SetVoxelRaw(Position pos, LevelVoxel voxel)
{
	this->array.get()[pos.y * this->size.x + pos.x] = voxel;
}

void Level::SetVoxelRaw(int offset, LevelVoxel voxel)
{
	this->array.get()[offset] = voxel;
}

LevelVoxel& Level::Voxel(Position pos)
{
	if (!IsInBounds(pos))
		throw GameException("Invalid position");
	return this->array.get()[pos.y * this->size.x + pos.x];
}

LevelVoxel& Level::VoxelRaw(Position pos)
{
	return this->array.get()[pos.y * this->size.x + pos.x];
}

LevelVoxel Level::GetVoxel(Position pos) const
{
	if (!IsInBounds(pos))
		return LevelVoxel::Rock;
	return this->array.get()[ pos.y * this->size.x + pos.x ];
}

LevelVoxel Level::GetVoxelRaw(Position pos) const
{
	return this->array.get()[pos.y * this->size.x + pos.x];
}

LevelVoxel Level::GetVoxelRaw(int address) const
{
	return this->array.get()[address];
}

void Level::GenerateDirtAndRocks()
{
	for(int y = 0; y<this->size.y; y++)
		for(int x = 0; x<this->size.x; x++) {
			auto& spot = this->Voxel({ x, y });
			if(spot == LevelVoxel::LevelGenRock)
				spot = LevelVoxel::Rock;
			else      
				spot = Random.Bool(500) ? LevelVoxel::DirtLow : LevelVoxel::DirtHigh;
		}
}

void Level::CreateBase(Position pos, TankColor color)
{
	if(color >= MAX_TANKS) 
		return;
	
	for(int y = -BASE_SIZE / 2; y<=BASE_SIZE/2; y++) {
		for(int x = -BASE_SIZE / 2; x<=BASE_SIZE/2; x++) 
		{
			Position pix = pos + Offset{ x, y };
			if(abs(x) == BASE_SIZE/2 || abs(y) == BASE_SIZE/2) 
			{	// Outline
				if(x >= -BASE_DOOR_SIZE/2 && x <= BASE_DOOR_SIZE/2) 
					continue;

				SetVoxel(pix, static_cast<LevelVoxel>(static_cast<char>(LevelVoxel::BaseMin) + color));
			}
			else
				SetVoxel(pix, LevelVoxel::Blank);
		}
	}
}

/* TODO: Rethink the method for adding bases, as the current method DEMANDS that
 *       you use MAX_TANKS tanks. */
void Level::CreateBases()
{
	for (TankColor i = 0; i < MAX_TANKS; i++) {
		CreateBase({ this->spawn[i].x, this->spawn[i].y }, i);
	}
}

Position Level::GetSpawn(TankColor color) const
{
	assert(color >= 0 && (int)color < this->spawn.size());
	return this->spawn[color];
}

void Level::SetSpawn(TankColor color, Position pos)
{
	assert(color >= 0 && (int)color < this->spawn.size());
	this->spawn[color] = pos;
}

bool Level::DigHole(Position pos)
{
	bool did_dig = false;
	
	for(int ty = pos.y - 3; ty<= pos.y+3; ty++)
		for(int tx = pos.x - 3; tx<= pos.x+3; tx++) {
			/* Don't go out-of-bounds: */
			LevelVoxel voxel = GetVoxel({ tx, ty });
			if (voxel != LevelVoxel::DirtHigh && voxel != LevelVoxel::DirtLow) 
				continue;
			
			/* Don't take out the corners: */
			if((tx==pos.x-3 || tx== pos.x+3) && (ty== pos.y-3 || ty== pos.y+3)) 
				continue;
			
			SetVoxel({ tx, ty }, LevelVoxel::Blank);
			did_dig = true;
		}
	
	return did_dig;
}

void Level::CommitAll() const
{
	for (int y = 0; y < this->size.y; y++) {
		for (int x = 0; x < this->size.x; x++) {
			CommitPixel({ x, y });
		}
	}
}

bool Level::IsInBounds(Position pos) const
{
	return !(pos.x < 0 || pos.y < 0 || pos.x >= this->size.x || pos.y >= this->size.y);
}


void Level::CommitPixel(Position pos) const
{
	drawBuffer->SetPixel(pos, GetVoxelColor(this->GetVoxel(pos))); 
}


/* TODO: This needs to be done in a different way, as this approach will take 
 * MAX_TANKS^2 time to do all collision checks for all tanks. It should only
 * take MAX_TANKS time. */
BaseCollision Level::CheckBaseCollision(Position pos, TankColor color)
{
	for(TankColor id = 0; id < MAX_TANKS; id++) {
		if(std::abs(this->spawn[id].x - pos.x) < BASE_SIZE/2 && std::abs(this->spawn[id].y - pos.y) < BASE_SIZE/2) {
			if (id == color)
				return BaseCollision::Yours;
			return BaseCollision::Enemy;
		}
	}
	
	return BaseCollision::None;
}

Color Level::GetVoxelColor(LevelVoxel voxel)
{
	if (voxel == LevelVoxel::DirtHigh)	   return color_dirt_hi;
	else if (voxel == LevelVoxel::DirtLow) return color_dirt_lo;
	else if (voxel == LevelVoxel::Rock)    return color_rock;
	else if (voxel == LevelVoxel::Blank)   return color_blank;
	else if (Voxels::IsBase(voxel))
		return color_tank[static_cast<char>(voxel) - static_cast<char>(LevelVoxel::BaseMin)][0];
	else {
		assert(!"Unknown voxel.");
		return {};
	}
}

/* Dumps a level into a BMP file: */
void Level::DumpBitmap(const char *filename) {
	BMPFile *f = gamelib_bmp_new(this->size.x, this->size.y);
	
	for(int y = 0; y< this->size.y; y++)
		for(int x = 0; x< this->size.x; x++) {
			Color color = Color(0,0,0);
			
			LevelVoxel val = this->GetVoxel({ x, y });
			gamelib_bmp_set_pixel(f, x, y, GetVoxelColor(val));
		}
	
	gamelib_bmp_finalize(f, filename);
}

