namespace Tunnerer.Core.Config;

using Tunnerer.Core.Types;

public readonly record struct ExplosionParams(int ShrapnelCount, float Speed, int Frames);

public static class Tweaks
{
    public static class System
    {
        public const string WindowTitle = "Tunnerer";
        public const string Version = "0.1 alpha";
    }

    public static class Perf
    {
        public const int TargetFps = 24;
        public const int SectorSize = 64;
        public const int ProjectileCompactThreshold = 5000;
        public static int ParallelismDegree => Math.Max(1, Environment.ProcessorCount);
    }

    public static class Screen
    {
        public static readonly Size WindowSize = new(1920, 1200);
        public static readonly Size RenderSurfaceSize = new(320, 200);
        public const float DrawStaticFuelThreshold = 0.2f;
        public const int PixelScale = 6;
        public const int HiResInitialQuality = 2; // 0=Low, 1=Medium, 2=High
        public const float HiResRenderBudgetMs = 8.0f;
        public const int HiResBudgetHysteresisFrames = 12;
    }

    public static class World
    {
        public static readonly TimeSpan AdvanceStep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / Perf.TargetFps);
        public const int MaxPlayers = 8;
        public static readonly TimeSpan DirtRecoverInterval = TimeSpan.FromMilliseconds(250);
        public const int DirtRecoverSpeed = 10;
        public const int DirtRegrowSpeed = 4;
        public const int DirtRegrowBlankModifier = 4;
        public const int DirtRegrowScorchedModifier = 1;
        public const int DecalDecaySpeed = 40;
        public const int HeatCooldownPerTick = 1;
        public const float HeatDiffuseRate = 0.18f;
        public const int DigThroughRockChance = 250;
        public static readonly TimeSpan RefreshLinkMapInterval = TimeSpan.FromMilliseconds(200);
        public const float MaximumLiveLinkDistance = 100f;
        public const float MaximumTheoreticalLinkDistance = 170f;
    }

    public static class Base
    {
        public const int MinDistance = 150;
        public const int BaseSize = 35;
        public const int DoorSize = 7;
        public const int InitialEnergy = 15000;
        public const int InitialHealth = 2000;
        public const int EnergyCapacity = 30000;
        public const int HealthCapacity = 2000;
        public const int MaterialDirtCapacity = 2000;
        public const int MaterialMineralsCapacity = 20000;
        public const int EnergyRegen = 100;
        public const int HealthRegen = 10;
        public const int HomeRechargeEnergy = 300;
        public const int HomeRechargeHealth = 3;
        public const int ForeignRechargeEnergy = 90;
        public const int ForeignRechargeHealth = 1;
        public const int HomeAbsorbDirt = 15;
        public const int HomeAbsorbMinerals = 15;
    }

    public static class Tank
    {
        public const int MaxLives = 3;
        public static readonly TimeSpan RespawnDelay = TimeSpan.FromSeconds(3);
        public const int TurretDelay = 3;
        public const int TurretLength = 4;
        public const int DigRadius = 3;
        public const int InitialEnergy = 24000;
        public const int InitialHealth = 1000;
        public const int EnergyCapacity = 24000;
        public const int HealthCapacity = 1000;
        public const int ResourceDirtCapacity = 10000;
        public const int ResourceMineralsCapacity = 10000;
        public const int IdleEnergyDrain = 3;
        public const int MoveEnergyDrain = 8;
        public const int ShootEnergyCost = 160;
        public const int EnergyPickupLow = 100;
        public const int EnergyPickupMedium = 200;
        public const int EnergyPickupHigh = 400;

        public const float HeatMax = 100f;
        public const float HeatDigPerPixel = 0.008f;
        public const float HeatShootPerShot = 1.8f;
        public const float HeatTerrainAbsorb = 0.008f;
        public const float HeatCoolPerFrame = 0.18f;
        public const float HeatBaseCoolBonus = 0.30f;
        public const float HeatOverheatThreshold = 70f;
        public const float HeatDamagePerFrame = 0.5f;
    }

    public static class Weapon
    {
        public const int CannonBulletSpeed = 3;
        public const float ConcreteBarrelSpeed = 2f;
        public const float DirtBarrelSpeed = 2f;
        public const int BulletDamage = 160;
    }

    public static class Machine
    {
        public const int ReactorEnergyCapacity = 10000;
        public const int ReactorHealthCapacity = 1000;
        public const int HarvesterIntervalMs = 500;
        public const int ChargerIntervalMs = 200;
        public const int HarvestRange = 20;
        public const int HarvesterDirtCost = 1000;
        public const int ChargerDirtCost = 500;
        public const int BoundingBoxHalfSize = 2;
    }

    public static class Explosion
    {
        public const float MadnessLevel = 1f;
        public const int ChanceToDestroyConcrete = 50;
        public const int ChanceToDestroyRock = 50;
        public const float SpeedVarianceBase = 0.1f;
        public const float SpeedVarianceRange = 0.9f;

        public static readonly ExplosionParams Dirt = new(ShrapnelCount: 10, Speed: 0.375f, Frames: 10);
        public static readonly ExplosionParams Normal = new(ShrapnelCount: 14, Speed: 0.56f, Frames: 13);
        public static readonly ExplosionParams Death = new(ShrapnelCount: 100, Speed: 0.25f, Frames: 72);
    }

    public static class Colors
    {
        public static readonly Color BaseOutline = new(40, 40, 40);
        public static readonly Color TurretBarrelTip = new(0xf3, 0xeb, 0x1c);
        public static readonly Color FireHot = new(0xff, 0x34, 0x08);
        public static readonly Color Concrete = new(0xba, 0xba, 0xcc);
        public static readonly Color DirtProjectile = new(0xaa, 0x50, 0x03);
        public static readonly Color Harvester = new(0x00, 0x88, 0x00);
        public static readonly Color Charger = new(0x4f, 0x4f, 0xff, 0xa0);
        public static readonly Color MachineTemplate = new(0x66, 0x66, 0x66, 0x80);
        public static readonly Color FailedInteraction = new(0xff, 0x34, 0x08);
        public static readonly Color InfoMarker = new(0xff, 0xff, 0x4a);
        public static readonly Color LinkLive = new(0xa0, 0xa0, 0x19);
        public static readonly Color LinkBlocked = new(0xa0, 0x30, 0x30);
    }

    public static class LevelGen
    {
        public const int BorderWidth = 30;
        public const int MaxDirtSpawnOdds = 300;
        public const int DirtSpawnProgression = 70;
        public const int DirtTargetPercent = 65;
        public const int TreeSize = 150;
        public const int MaxPlayers = 2;
        public const int MinSpawnDistanceSq = 150 * 150;
        public const int SmoothingSteps = -1; // -1 = smooth until convergence (matches C++)
        public static int TargetDirtAmount(Size size) => size.X * size.Y * DirtTargetPercent / 100;
    }
}
