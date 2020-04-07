#pragma once

#include <thread>
#include <algorithm>
#include <types.h>
#include <chrono>

namespace tweak {

namespace system
{
    constexpr char WindowTitle[] = "Diggerer";
    constexpr char Version[] = "0.1 alpha";
} // namespace system

namespace screen
{
	/* The default size of the window: */
	constexpr Size WindowSize = { 640, 400 };
    /* The virtual resolution of the game. (IE: How many blocks tall/wide) */
    constexpr Size RenderSurfaceSize = {160, 100};

}


namespace perf {
	constexpr int parallelism_percent = 100;
	inline unsigned int parallelism_degree = std::max(1u, std::thread::hardware_concurrency() * parallelism_percent / 100);

	/* The desired speed in frames per second: */
	constexpr int TargetFps = 24;
	constexpr std::chrono::milliseconds AdvanceStep{ 1000 / TargetFps };

}

namespace world
{
    constexpr int DirtRecoverSpeed = 2; /* Average delay before growing finishes and new dirt is formed. More is faster. */
    constexpr int DirtRegrowSpeed = 5;  /* Average delay before it starts growing back. More is faster.*/
    constexpr int DigThroughRockChance = 250; /* Chance to dig through rock with torch of out 1000 */
    /* The minimum distance between two tanks in the world. If this is set too high,
     * then the level generator may start throwing exceptions: */
    constexpr int MinBaseDistance = 150;
    /* Various base sizes: */
    #define BASE_SIZE                      35
    #define BASE_DOOR_SIZE                 7

} // namespace world


constexpr int MaxPlayers = 8;

namespace tank {

	constexpr int MaxLives = 3;
	constexpr int RespawnDelay = perf::TargetFps * 3;
	
	/* The number of frames to wait in between shots: */
	constexpr int TurretDelay = 3;
	/* The maximum number of bullets allowed from a given tank: */
	constexpr int BulletMax = 6;

	/* Various constants for energy calculation: */
	constexpr int StartingFuel = 24000;
	constexpr int ShootCost = -160;
	constexpr int MoveCost = -8;
	constexpr int IdleCost = -3;
	constexpr int HomeChargeSpeed = 300;
	constexpr int EnemyChargeSpeed = 90;
				  
	/* Various constants for health calculation: */
	constexpr int StartingShield = 1000;
	constexpr int ShotDamage = -160;
	constexpr int HomeHealSpeed = 3;

    constexpr int TurretLength = 4;
}

/* Constants for drawing static: (The bottom 3 constants are out of 1000) */
#define STATIC_THRESHOLD               (tweak::tank::StartingFuel/5)
#define STATIC_TRANSPARENCY            200
#define STATIC_BLACK_BAR_ODDS          500
#define STATIC_BLACK_BAR_SIZE          500


namespace control
{
    constexpr int GamePadMovementThreshold = 10000;
    constexpr int GamePadAimSensitivity = 30;
    constexpr int GamePadAimThreshold = 3000;
    constexpr float GamePadCrosshairRadius = 25.f;
}

namespace weapon
{
	/* The speed in pixels/frame of bullets: */
	constexpr int CannonBulletSpeed = 3;
    constexpr DurationFrames CannonCooldown = DurationFrames{3};
    constexpr float ConcreteBarrelSpeed = 2.f;
    constexpr DurationFrames ConcreteSprayCooldown = DurationFrames{3};
    constexpr int ConcreteDetonationDistance = 3;
    constexpr float DirtBarrelSpeed = 2.f;
    constexpr int DirtDetonationDistance = 3;
}

namespace explosion
{
    constexpr float MadnessLevel = 1.f;
    constexpr int ChanceToDestroyConcrete = 50;
    constexpr int ChanceToDestroyRock = 50;
    }

namespace explosion::dirt
{
    constexpr int ShrapnelCount = 10;
    constexpr float Speed = 0.375f;
    constexpr int Frames = 10;
} // namespace explosion::dirt
namespace explosion::normal
{
    constexpr int ShrapnelCount = 14;
    constexpr float Speed = 0.56f;
    constexpr int Frames = 13;
} // namespace explosion::normal
namespace explosion::death
{
    constexpr int ShrapnelCount = 100;
    constexpr float Speed = 0.25f;
    constexpr int Frames = 72;
} // namespace explosion::death
/* Characters used in level structures for things: */

/* Default to keeping memory stats: */
#ifndef _MEM_STATS
#define _MEM_STATS
#endif

 
}