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
        public static readonly Size WindowSize = new(1920, 1080);
        public static readonly Size RenderSurfaceSize = new(320, 200);
        public const float DrawStaticFuelThreshold = 0.2f;
        public const int PixelScale = 6;
        public const int NativeContinuousSampleLow = 1;
        public const int NativeContinuousSampleMedium = 2;
        public const int NativeContinuousSampleHigh = 4;
        public const float NativeContinuousEdgeSoftness = 0.18f;
        public const float NativeContinuousBoundaryBlend = 0.98f;
        public const float NativeContinuousRenderBudgetMs = 5.0f;
        public const int NativeContinuousBudgetHysteresisFrames = 10;
        public const float NativeContinuousBudgetUnderThreshold = 0.65f;
        public const int NativeContinuousRecoveryFramesMultiplier = 20;

        // GPU post-processing / lighting tuning
        public const float PostBloomThreshold = 0.56f;
        public const float PostBloomStrength = 1.35f;
        public const float PostBloomWeightCenter = 0.42f;
        public const float PostBloomWeightAxis = 0.20f;
        public const float PostBloomWeightDiagonal = 0.12f;
        public const float PostVignetteStrength = 0.18f;
        public const float PostVignetteInnerRadius = 0.35f;
        public const float PostVignetteOuterRadius = 0.95f;
        public const float PostTerrainEdgeLightStrength = 4.00f;
        public const float PostTerrainEdgeLightBias = 0.00f;
        public const float PostTerrainHeatThreshold = 0.10f;
        public const float PostTerrainMaskEdgeStrength = 0.18f;
        public const float PostTerrainMaskCaveDarken = 0.12f;
        public const float PostTerrainMaskSolidLift = 0.04f;
        public const float PostTerrainMaskOutlineDarken = 0.26f;
        public const float PostTerrainMaskRimLift = 0.07f;
        public const float PostTerrainMaskBoundaryScale = 2.4f;
        public const float PostMaterialEmissiveEnergyR = 0.7843f;
        public const float PostMaterialEmissiveEnergyG = 0.8627f;
        public const float PostMaterialEmissiveEnergyB = 0.2353f;
        public const float PostMaterialEmissiveScorchedR = 0.7059f;
        public const float PostMaterialEmissiveScorchedG = 0.3137f;
        public const float PostMaterialEmissiveScorchedB = 0.1176f;
        public const float PostMaterialEmissiveEnergyStrength = 0.95f;
        public const float PostMaterialEmissiveScorchedStrength = 0.32f;
        public const float PostMaterialEmissivePulseFreq = 3.0f;
        public const float PostMaterialEmissivePulseMin = 0.8f;
        public const float PostMaterialEmissivePulseRange = 0.2f;
        public const float PostTankHeatGlowR = 0.78f;
        public const float PostTankHeatGlowG = 0.24f;
        public const float PostTankHeatGlowB = 0.04f;
        public const bool PostTankHeatDistortionEnabled = false;
        public const float PostTankHeatGlowMinHeat = 1.5f;
        public const float PostTankHeatGlowBaseRadius = 2.5f;
        public const float PostTankHeatGlowScaleRadius = 2.5f;
        public const float PostTerrainHeatGlowR = 1.0000f;
        public const float PostTerrainHeatGlowG = 0.4500f;
        public const float PostTerrainHeatGlowB = 0.0500f;
        public const byte PostEmissiveEnergyLow = 150;
        public const byte PostEmissiveEnergyMedium = 190;
        public const byte PostEmissiveEnergyHigh = 230;
        public const byte PostEmissiveScorchedHigh = 64;
        public const byte PostEmissiveScorchedLow = 32;

        // Directional lighting (matches CPU path: top-left, slightly forward)
        public const float LightDirX = -0.35f;
        public const float LightDirY = -0.55f;
        public const float LightDirZ = 0.70f;
        public const float LightAmbient = 0.22f;
        public const float LightDiffuseWeight = 0.68f;
        public const float LightNormalStrength = 2.2f;
        public const float LightShininess = 8f;
        public const float LightSpecularIntensity = 0.10f;
        public const float LightMicroNormalStrength = 0.35f;
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
        public const int DigThroughRockChance = 250;
        public static readonly TimeSpan RefreshLinkMapInterval = TimeSpan.FromMilliseconds(200);
        public const float MaximumLiveLinkDistance = 100f;
        public const float MaximumTheoreticalLinkDistance = 170f;

        // Physically-inspired material heat exchange model.
        public static bool EnableMaterialHeatExchange => true;
        // Legacy sink toggle; conservative mode keeps this off.
        public static bool EnableThermalAmbientExchange => false;
        public static bool EnableStoneAmbientExchange { get; set; } = true;
        public static bool EnableConservativeAirField => true;
        public const float ThermalDt = 2.0f;
        public const float ThermalAmbientTemperature = 0f;

        // Effective heat capacities (higher means temperature changes slower).
        public const float ThermalCapacityAir = 0.8f;
        public const float ThermalCapacityDirt = 1.4f;
        public const float ThermalCapacityStone = 2.6f;
        public const float ThermalCapacityBase = 2.6f;
        public const float ThermalCapacityConstantEnergy = 2.0f;

        // Pairwise transmission speed (conductance).
        public const float ThermalKAirAir = 0.240f;
        public const float ThermalKAirDirt = 0.110f;
        public const float ThermalKAirStone = 0.220f;
        public const float ThermalKAirBase = 0.450f;
        public const float ThermalKDirtDirt = 0.160f;
        public const float ThermalKDirtStone = 0.200f;
        public const float ThermalKDirtBase = 0.350f;
        public const float ThermalKStoneStone = 0.300f;
        public const float ThermalKStoneBase = 0.450f;
        public const float ThermalKBaseBase = 0.200f;
        public const float ThermalKAirConstantEnergy = 0.550f;
        public const float ThermalKDirtConstantEnergy = 0.350f;
        public const float ThermalKStoneConstantEnergy = 0.450f;
        public const float ThermalKBaseConstantEnergy = 0.450f;
        public const float ThermalKConstantEnergyConstantEnergy = 0.300f;

        // Air-field coupling knobs for conservative air/terrain exchange.
        public const float ThermalAirCellCoupling = 0.120f;
        public const float ThermalAirNeighborCoupling = 0.060f;

        public const float ThermalFixedBaseTemperature = 0f;
        public const float ThermalFixedConstantEnergyTemperature = 0f;
        public const float ThermalKAmbientAir = 0.0120f;
        public const float ThermalKAmbientDirt = 0.0120f;
        public const float ThermalKAmbientStone = 0.0100f;
        public const float ThermalKAmbientBase = 0.0000f;
        public const float ThermalKAmbientConstantEnergy = 0.0000f;
        public const int ThermalActiveTileSize = 32;
        public const float ThermalSparseFallbackCoverage = 0.45f;
        public const int ThermalParallelRegionThreshold = 4;
        public const int ThermalMaxWorkers = 0; // 0 => default scheduler/CPU count
        public const float ThermalActiveTemperatureThreshold = 0.5f;
        public const float ThermalArtificialCoolingPerFrame = 0f;
    }

    public static class Base
    {
        public const int MinDistance = 150;
        public const int BaseSize = 35;
        public const int DoorSize = 7;
        public const int InitialHeat = 0;
        public const int InitialHealth = 2000;
        public const int HeatCapacity = 100;
        public const int HealthCapacity = 2000;
        public const int MaterialDirtCapacity = 2000;
        public const int MaterialMineralsCapacity = 20000;
        public const int HeatCooldown = 2;
        public const int HealthRegen = 10;
        public const int AreaCoolingInnerHeatTotal = 6;
        public const int AreaCoolingMidHeatTotal = 3;
        public const int AreaCoolingOuterHeatTotal = 1;
        public const int AreaCoolingInnerAirTotal = 8;
        public const int AreaCoolingMidAirTotal = 4;
        public const int AreaCoolingOuterAirTotal = 2;
        public const int HomeCooldownHeat = 6;
        public const int HomeRechargeHealth = 3;
        public const int ForeignCooldownHeat = 2;
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
        public const int InitialHeat = 0;
        public const int InitialHealth = 1000;
        public const int HeatCapacity = 100;
        public const int HealthCapacity = 1000;
        public const int ResourceDirtCapacity = 10000;
        public const int ResourceMineralsCapacity = 10000;
        public const int IdleHeatGain = 0;
        public const int MoveHeatGain = 0;
        public const float ShootHeatGain = 2.0f;
        public const int CoolingPickupLow = 8;
        public const int CoolingPickupMedium = 14;
        public const int CoolingPickupHigh = 20;

        public const float HeatMax = 100f;
        public const float HeatDigPerPixel = 0.008f;
        public const float HeatShootPerShot = 0.25f;
        public const float HeatTerrainAbsorb = 0.004f;
        public const float HeatCoolPerFrame = 0.12f;
        public const float HeatBaseCoolBonus = 0.20f;
        public const float HeatColdCoolingBoost = 0.60f;
        public const int TorchTerrainHeatAmount = 8;
        public const int TorchRockHeatAmount = 14;
        public const int TorchRockHeatRadius = 3;
        public const float HeatAmbientOutsideBase = 0f;
        public const float TankHeatCapacity = 20f;
        public const float TerrainHeatCapacity = 80f;
        public const float TankTerrainConductance = 2.0f;
        public const float TankAmbientConductance = 0.2f;
        public const float TankBaseConductance = 0.4f;
        public const float HeatSafeMax = 100f;
        public const float OverheatDamagePerDegree = 0.5f;
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
        public const int ReactorHeatCapacity = 100;
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
        public const float BulletExplosionHeatScale = 1.5f;
        public const float DeathHeatScale = 2.0f;

        public const int BulletHeatAmount = 3;
        public const int BulletHeatRadius = 7;
        public const int ShrapnelHitHeat = 2;
        public const int ShrapnelDigHeatAmount = 2;
        public const int ShrapnelDigHeatRadius = 5;
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

        public const int EnergyVeinCountDivisor = 40;
        public const int EnergyVeinMinLength = 5;
        public const int EnergyVeinMaxLength = 16;
        public const int RuinAreaPerRuin = 16000;
        public const int RuinWallMinLength = 2;
        public const int RuinWallMaxLength = 5;
    }
}
