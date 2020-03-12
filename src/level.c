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
		this->array.get()[i] = 1;
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
		return ROCK;
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

void Level::CreateDirtAndRocks()
{
	for(int y = 0; y<this->size.y; y++)
		for(int x = 0; x<this->size.x; x++) {
			char& spot = this->Voxel({ x, y });
			if(spot)  spot = ROCK;
			else      spot = Random.Bool(500) ? DIRT_LO : DIRT_HI;
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

				SetVoxel(pix, static_cast<char>(BASE + color));
			}
			else
				SetVoxel(pix, BLANK);
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
	assert(color >= 0 && color < this->spawn.size());
	return this->spawn[color];
}

void Level::SetSpawn(TankColor color, Position pos)
{
	assert(color >= 0 && color < this->spawn.size());
	this->spawn[color] = pos;
}

bool Level::DigHole(Position pos)
{
	bool did_dig = false;
	
	for(int ty = pos.y - 3; ty<= pos.y+3; ty++)
		for(int tx = pos.x - 3; tx<= pos.x+3; tx++) {
			/* Don't go out-of-bounds: */
			LevelVoxel voxel = GetVoxel({ tx, ty });
			if (voxel != DIRT_HI && voxel != DIRT_LO) 
				continue;
			
			/* Don't take out the corners: */
			if((tx==pos.x-3 || tx== pos.x+3) && (ty== pos.y-3 || ty== pos.y+3)) 
				continue;
			
			SetVoxel({ tx, ty }, BLANK);
			did_dig = true;
		}
	
	return did_dig;
}

void Level::CommitAll() const
{
	for(int y=0; y<this->size.y; y++)
		for(int x=0; x<this->size.x; x++) {
			char val = this->GetVoxel({ x, y });
			switch(val) {
				case ROCK:    drawBuffer->SetPixel({x,y}, color_rock); break;
				case DIRT_HI: drawBuffer->SetPixel({x,y}, color_dirt_hi); break;
				case DIRT_LO: drawBuffer->SetPixel({x,y}, color_dirt_lo); break;
				case BLANK:   drawBuffer->SetPixel({x,y}, color_blank); break;
				default:
					/* Else, this is most likely a base: */
					int color = val - BASE;
					if(color < MAX_TANKS)
						drawBuffer->SetPixel({x,y}, color_tank[color][0]); break;
			}
		}
}

bool Level::IsInBounds(Position pos) const
{
	return !(pos.x < 0 || pos.y < 0 || pos.x >= this->size.x || pos.y >= this->size.y);
}


void Level::CommitPixel(Position pos) const
{
	char val = this->GetVoxel(pos);
	
	switch(val) {
		case ROCK:    drawBuffer->SetPixel(pos, color_rock); break;
		case DIRT_HI: drawBuffer->SetPixel(pos, color_dirt_hi); break;
		case DIRT_LO: drawBuffer->SetPixel(pos, color_dirt_lo); break;
		case BLANK:   drawBuffer->SetPixel(pos, color_blank); break;
		default:
			/* Else, this is most likely a base: */
			int color = val - BASE;
			if(color < MAX_TANKS)
				drawBuffer->SetPixel(pos, color_tank[color][0]); break;
	}
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


/* Dumps a level into a BMP file: */
void Level::DumpBitmap(const char *filename) {
	BMPFile *f = gamelib_bmp_new(this->size.x, this->size.y);
	
	for(int y = 0; y< this->size.y; y++)
		for(int x = 0; x< this->size.x; x++) {
			Color color = Color(0,0,0);
			
			char val = this->GetVoxel({ x, y });
			
			if     (val == DIRT_HI) color = color_dirt_hi;
			else if(val == DIRT_LO) color = color_dirt_lo;
			else if(val == ROCK)    color = color_rock;
			else if(val == BLANK)   color = color_blank;
			else if(val-BASE < MAX_TANKS && val-BASE >= 0)
				color = color_tank[val-BASE][0];
			
			gamelib_bmp_set_pixel(f, x, y, color);
		}
	
	gamelib_bmp_finalize(f, filename);
}

