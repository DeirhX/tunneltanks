namespace TunnelTanks.Core.Config;

using TunnelTanks.Core.Types;

public static class Tweaks
{
    public static class System
    {
        public const string WindowTitle = "TunnelTanks";
        public const string Version = "0.1 alpha";
    }

    public static class Perf
    {
        public const int TargetFps = 24;
        public const int SectorSize = 64;
        public static int ParallelismDegree => Math.Max(1, Environment.ProcessorCount);
    }

    public static class Screen
    {
        public static readonly Size WindowSize = new(1920, 1200);
        public static readonly Size RenderSurfaceSize = new(320, 200);
        public const float DrawStaticFuelThreshold = 0.2f;
    }

    public static class World
    {
        public static readonly TimeSpan AdvanceStep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / Perf.TargetFps);
        public const int MaxPlayers = 8;
        public static readonly TimeSpan DirtRecoverInterval = TimeSpan.FromMilliseconds(250);
        public const int DirtRecoverSpeed = 10;
        public const int DirtRegrowSpeed = 4;
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
    }

    public static class Tank
    {
        public const int MaxLives = 3;
        public static readonly TimeSpan RespawnDelay = TimeSpan.FromSeconds(3);
        public const int TurretDelay = 3;
        public const int BulletMax = 6;
        public const int TurretLength = 4;
    }

    public static class Weapon
    {
        public const int CannonBulletSpeed = 3;
        public const float ConcreteBarrelSpeed = 2f;
        public const float DirtBarrelSpeed = 2f;
    }

    public static class Explosion
    {
        public const float MadnessLevel = 1f;
        public const int ChanceToDestroyConcrete = 50;
        public const int ChanceToDestroyRock = 50;

        public static class Dirt { public const int ShrapnelCount = 10; public const float Speed = 0.375f; public const int Frames = 10; }
        public static class Normal { public const int ShrapnelCount = 14; public const float Speed = 0.56f; public const int Frames = 13; }
        public static class Death { public const int ShrapnelCount = 100; public const float Speed = 0.25f; public const int Frames = 72; }
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
