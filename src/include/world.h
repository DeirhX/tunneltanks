#pragma once

#include "tanklist.h"
#include "projectile_list.h"
#include "level.h"

class World
{
public:
	std::unique_ptr<TankList> tank_list;
	std::unique_ptr<ProjectileList> projectiles;
	std::unique_ptr<Level> level;
public:
	void Advance(class DrawBuffer* drawBuffer);
private:
	std::chrono::microseconds regrow_elapsed = {};
	std::chrono::microseconds regrow_average = {};
	int advance_count = 0;

	void RegrowPass();
};

