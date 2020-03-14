#pragma once

#include <thread>
#include <algorithm>
#include <types.h>
#include <chrono>

namespace tweak {
/*
 * Most of the game engine's arbitrary limits are stored in here:
 */
	namespace perf {
		constexpr int parallelism_percent = 100;
		inline unsigned int parallelism_degree = std::max(1u, std::thread::hardware_concurrency() * parallelism_percent / 100);

		/* The desired speed in frames per second: */
		constexpr int TargetFps = 24;
		constexpr std::chrono::milliseconds AdvanceStep{ 1000 / TargetFps };

	}

    constexpr int DirtRecoverSpeed = 5; /* Average delay before growing finishes and new dirt is formed. More is faster. */
    constexpr int DirtRegrowSpeed = 10; /* Average delay before it starts growing back. More is faster.*/

	namespace screen
	{
		/* The default size of the window: */
		constexpr Size size = { 640, 400 };
		constexpr int max_windows = 4;
		constexpr int max_status = 4;
		constexpr int max_bitmaps = 4;

		/* Various attributes of the status bar: */
		constexpr int status_height = 11;
		constexpr int status_border = 1;
	}


/* Window title / version string: */
#define WINDOW_TITLE                   "TunnelTanks"
#define VERSION                        "0.5 alpha"

/* The virtual resolution of the game. (IE: How many blocks tall/wide) */
#define GAME_WIDTH                     160
#define GAME_HEIGHT                    100
constexpr Size GameSize = { GAME_WIDTH, GAME_HEIGHT };


/* The minimum distance between two tanks in the world. If this is set too high,
 * then the level generator may start throwing exceptions: */
#define MIN_SPAWN_DIST                 150

namespace tank {
	    /* The number of frames to wait in between shots: */
	constexpr int BulletDelay = 3;

	/* The maximum number of bullets allowed from a given tank: */
	constexpr int BulletMax = 6;

	/* The speed in pixels/frame of bullets: */
	constexpr int BulletSpeed = 3;

	/* The maximum number of tanks: */
	#define MAX_TANKS                      8

	/* Various constants for energy calculation: */
	#define TANK_STARTING_FUEL             24000
	#define TANK_SHOOT_COST                -160
	#define TANK_MOVE_COST                 -8
	#define TANK_IDLE_COST                 -3
	#define TANK_HOME_CHARGE               300
	#define TANK_ENEMY_CHARGE              90

	/* Various constants for health calculation: */
	#define TANK_STARTING_SHIELD           1000
	#define TANK_SHOT_DAMAGE               -160
	#define TANK_HOME_HEAL                 3

}
/* Constants for drawing static: (The bottom 3 constants are out of 1000) */
#define STATIC_THRESHOLD               (TANK_STARTING_FUEL/5)
#define STATIC_TRANSPARENCY            200
#define STATIC_BLACK_BAR_ODDS          500
#define STATIC_BLACK_BAR_SIZE          500

/* Various base sizes: */
#define BASE_SIZE                      35
#define BASE_DOOR_SIZE                 7

/* The starting number of items in the Projectile Buffer's cache: */
#define PROJECTILE_BUFFER_START_SIZE   32

/* The radius/number/ttl of particles in various special effects: */
/* TODO: radius would probably be better named as 'speed' or the like... */

#define EXPLOSION_DIRT_COUNT           10
#define EXPLOSION_DIRT_RADIUS          12
#define EXPLOSION_DIRT_TTL             10

#define EXPLOSION_HURT_COUNT           14
#define EXPLOSION_HURT_RADIUS          18
#define EXPLOSION_HURT_TTL             13

#define EXPLOSION_DEATH_COUNT          100
#define EXPLOSION_DEATH_RADIUS         8
#define EXPLOSION_DEATH_TTL            72

/* Characters used in level structures for things: */

/* Default to keeping memory stats: */
#ifndef _MEM_STATS
#define _MEM_STATS
#endif

 
}