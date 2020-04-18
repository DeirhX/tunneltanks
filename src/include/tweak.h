#pragma once

#include <thread>
#include <algorithm>
#include <types.h>
#include <chrono>

#include "duration.h"
#include "resources.h"

#define _MEM_STATS

namespace tweak
{
    

namespace system
{
    constexpr char WindowTitle[] = "Diggerer";
    constexpr char Version[] = "0.1 alpha";
} // namespace system

namespace perf
{
	constexpr int parallelism_percent = 100;
	inline unsigned int parallelism_degree = std::max(1u, std::thread::hardware_concurrency() * parallelism_percent / 100);

	/* The desired speed in frames per second: */
	constexpr int TargetFps = 24;
}

namespace world
{
    using namespace std::literals::chrono_literals;

	constexpr std::chrono::microseconds AdvanceStep{1'000'000 / perf::TargetFps};
    constexpr int MaxPlayers = 8;

    constexpr auto DirtRecoverInterval = 250ms; /* Perform the recovery queries only once per this interval */
    constexpr int DirtRecoverSpeed = 10; /* Average delay before growing finishes and new dirt is formed. More is faster. */
    constexpr int DirtRegrowSpeed = 4;  /* Average delay before it starts growing back. More is faster.*/
    constexpr int DigThroughRockChance = 250; /* Chance to dig through rock with torch of out 1000 */

    constexpr std::chrono::microseconds LinkReactorsInterval = 200ms;
    constexpr float MaximumLiveLinkDistance = 100.f;
    constexpr float MaximumTheoreticalLinkDistance = 170.f;
    } // namespace world

namespace base
{
    /* The minimum distance between two tanks in the world. If this is set too high,
     * then the level generator may start throwing exceptions: */
    constexpr int MinDistance = 150;
    constexpr int BaseSize = 35;
    constexpr int DoorSize = 7;
    constexpr Reactor Reactor = {15000_energy, 2000_health, {{30000_energy, 2000_health}}};
    constexpr MaterialContainer MaterialContainer = {0_dirt, 0_minerals, {{20000_dirt, 20000_minerals}}};
    constexpr ReactorState ReactorRecoveryRate = {100_energy, 10_health};
    constexpr MaterialAmount MaterialsAbsorbRate = {10_dirt, 10_minerals};
}

namespace tank {

    using namespace std::literals::chrono_literals;

    constexpr int MaxLives = 3;
    constexpr std::chrono::milliseconds RespawnDelay = 3000ms;

    /* The number of frames to wait in between shots: */
    constexpr int TurretDelay = 3;
    /* The maximum number of bullets allowed from a given tank: */
    constexpr int BulletMax = 6;

    /* Various constants for energy calculation: */
    constexpr MaterialCapacity ResourcesMax = {{10000_dirt, 10000_minerals}};
    constexpr EnergyAmount StartingEnergy = 24000_energy;
    constexpr EnergyAmount ShootCost = 160_energy;
    constexpr EnergyAmount MoveCost = 8_energy;
    constexpr EnergyAmount IdleCost = 3_energy;
    constexpr EnergyAmount HomeChargeSpeed = 300_energy;
    constexpr EnergyAmount EnemyChargeSpeed = 90_energy;

    /* Various constants for health calculation: */
    constexpr HealthAmount StartingShield = 1000_health;
    constexpr HealthAmount ShotDamage = 160_health;
    constexpr HealthAmount HomeHealSpeed = 3_health;
    constexpr HealthAmount EnemyHealSpeed = 1_health;

    constexpr Reactor DefaultTankReactor = {
        StartingEnergy, StartingShield,
        ReactorCapacity{{StartingEnergy, StartingShield}}};
    constexpr int TurretLength = 4;
}

namespace screen
{
	/* The default size of the window: */
	constexpr Size WindowSize = { 640, 400 };
    /* The virtual resolution of the game. (IE: How many blocks tall/wide) */
    constexpr Size RenderSurfaceSize = {160, 100};

    /* Constants for drawing static: (The bottom 3 constants are out of 1000) */
    constexpr int DrawStaticFuelThreshold = (tweak::tank::StartingEnergy.amount / 5);
    constexpr int DrawStaticTransparency = 200;
    constexpr int DrawStaticBlackBarOdds = 500;
    constexpr int DrawStaticBlackBarSize = 500;
 }

namespace control
{
    constexpr int GamePadMovementThreshold = 10000;
    constexpr int GamePadAimSensitivity = 30;
    constexpr int GamePadAimThreshold = 3000;
    constexpr float GamePadCrosshairRadius = 25.f;
}

namespace weapon
{
    using namespace std::literals::chrono_literals;

	/* The speed in pixels/frame of bullets: */
	constexpr int CannonBulletSpeed = 3;
    constexpr Duration CannonCooldown = Duration{100ms};
    constexpr float ConcreteBarrelSpeed = 2.f;
    constexpr Duration ConcreteSprayCooldown = Duration{100ms};
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

namespace rules
{
    using namespace std::literals::chrono_literals;

    constexpr MaterialAmount HarvesterCost = 100_dirt;
    constexpr MaterialAmount MinerCost = {1000_dirt, 50_minerals};
    constexpr std::chrono::milliseconds HarvestTimer = 500ms;
    constexpr int HarvestMaxRange = 20;
    constexpr MaterialAmount ChargerCost = 500_dirt;
    constexpr std::chrono::milliseconds ChargeTimer = 200ms;
    constexpr int ChargeMaxRange = 20;

    constexpr Reactor DefaultMachineReactor = {0_energy, 1000_health, ReactorCapacity{{10000_energy, 1000_health}}};
    constexpr Reactor HarvesterReactor = {0_energy, 1000_health, ReactorCapacity{{10000_energy, 1000_health}}};
    constexpr Reactor MinerReactor = {0_energy, 1000_health, ReactorCapacity{{10000_energy, 1000_health}}};
    constexpr Reactor ChargerReactor = {0_energy, 1000_health, ReactorCapacity{{10000_energy, 1000_health}}};

    }

 
}