#pragma once
#include <types.h>
#include <memory>
#include "levelgen.h"
#include "world.h"

struct GameDataConfig {
	LevelGenerator level_generator;
	Size size;
	bool is_debug;
	bool is_fullscreen;
	int player_count;
	int rand_seed;
};

struct Game {
	int is_active = false;
	int is_debug = false;
	
	GameDataConfig config = {};

	std::unique_ptr<class Level> level;
	std::unique_ptr<class DrawBuffer> draw_buffer;
	std::unique_ptr<class Screen> screen;
	World world;
public:
	Game(GameDataConfig config);
	~Game();

	bool AdvanceStep();
private:
	void RegrowPass();
};


