#pragma once
#include <types.h>
#include <memory>
#include "levelgen.h"
#include "world.h"

struct GameConfig {
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
	
	GameConfig config = {};

	std::unique_ptr<class DrawBuffer> draw_buffer;
	std::unique_ptr<class Screen> screen;
	World world;

	std::unique_ptr<class GameMode> mode;
public:
	Game(GameConfig config);
	~Game();

	bool AdvanceStep();
private:
};


class GameMode
{
protected:
	Screen* screen;
	World* world;
public:
	GameMode(Screen* screen, World* world) : screen(screen), world(world) {}
	~GameMode() { assert(!screen && !world || !"Didn't tear it down"); }
	
	virtual void TearDown() = 0;

	static void AssumeAIControl(TankList* tl, Level* lvl, TankColor starting_id);
};

class SinglePlayerMode : public GameMode
{
private:
	SinglePlayerMode(Screen* screen, World* world) : GameMode(screen, world) {}
public:
	static std::unique_ptr<SinglePlayerMode> Setup(Screen* screen, World* world);
	void TearDown() override;
};

class LocalTwoPlayerMode : public GameMode
{
private:
	LocalTwoPlayerMode(Screen* screen, World* world) : GameMode(screen, world) {}
public:
	static std::unique_ptr<LocalTwoPlayerMode> Setup(Screen* screen, World* world);
	void TearDown() override;
};
