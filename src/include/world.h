#pragma once

#include "tanklist.h"
#include "projectile_list.h"
#include "level.h"

class World
{
	class Game* game;

	std::unique_ptr<TankList> tank_list;
	std::unique_ptr<ProjectileList> projectile_list;
	std::unique_ptr<Level> level;
public:
	World(Game* game, 
		std::unique_ptr<TankList>&& tank_list, 
		std::unique_ptr<ProjectileList>&& projectile_list, 
		std::unique_ptr<Level>&& level ):
	game(game),
	tank_list(std::move(tank_list)),
	projectile_list(std::move(projectile_list)),
	level(std::move(level))
	{
	}
	void Advance(class DrawBuffer* drawBuffer);

	TankList* GetTankList() { return this->tank_list.get(); }
	ProjectileList* GetProjectileList() { return this->projectile_list.get(); }
	Level* GetLevel() { return this->level.get(); }
	void GameIsOver();
private:
	std::chrono::microseconds regrow_elapsed = {};
	std::chrono::microseconds regrow_average = {};
	int advance_count = 0;

	void RegrowPass();
};

