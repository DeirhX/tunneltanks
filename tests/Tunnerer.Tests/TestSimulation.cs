namespace Tunnerer.Tests;

using Tunnerer.Core;
using Tunnerer.Core.Input;
using Tunnerer.Core.Types;

internal static class TestSimulation
{
    public static readonly ControllerOutput Idle = default;

    public static ControllerOutput Move(int x, int y) =>
        new() { MoveSpeed = new Offset(x, y) };

    public static ControllerOutput Aim(float x, float y) =>
        new() { AimDirection = new DirectionF(x, y) };

    public static ControllerOutput MoveAndAim(int moveX, int moveY, float aimX, float aimY, bool shootPrimary = false) =>
        new()
        {
            MoveSpeed = new Offset(moveX, moveY),
            AimDirection = new DirectionF(aimX, aimY),
            ShootPrimary = shootPrimary,
        };

    public static void Advance(World world, int frames, ControllerOutput inputForTank0)
    {
        for (int i = 0; i < frames; i++)
            world.Advance(tankIndex => tankIndex == 0 ? inputForTank0 : Idle);
    }

    public static void Advance(World world, int frames, Func<int, ControllerOutput> inputForTank0ByFrame)
    {
        for (int i = 0; i < frames; i++)
        {
            var input = inputForTank0ByFrame(i);
            world.Advance(tankIndex => tankIndex == 0 ? input : Idle);
        }
    }

    public static void AdvanceIdle(World world, int frames)
    {
        for (int i = 0; i < frames; i++)
            world.Advance(_ => Idle);
    }

    public static ulong ComputeWorldStateHash(World world)
    {
        const ulong offset = 1469598103934665603UL;
        ulong hash = offset;

        static ulong Mix(ulong h, uint value)
        {
            const ulong fnvPrime = 1099511628211UL;
            h ^= value;
            h *= fnvPrime;
            return h;
        }

        var terrain = world.Terrain.Data;
        for (int i = 0; i < terrain.Length; i++)
            hash = Mix(hash, (uint)terrain[i]);

        hash = Mix(hash, (uint)world.AdvanceCount);
        hash = Mix(hash, (uint)world.Projectiles.Count);
        hash = Mix(hash, (uint)world.Machines.Machines.Count);
        hash = Mix(hash, (uint)world.LinkMap.Points.Count);
        hash = Mix(hash, (uint)world.LinkMap.Links.Count);

        var tanks = world.TankList.Tanks;
        hash = Mix(hash, (uint)tanks.Count);
        for (int i = 0; i < tanks.Count; i++)
        {
            var t = tanks[i];
            hash = Mix(hash, (uint)t.Position.X);
            hash = Mix(hash, (uint)t.Position.Y);
            hash = Mix(hash, (uint)t.Direction);
            hash = Mix(hash, (uint)t.LivesLeft);
            hash = Mix(hash, (uint)(int)t.Reactor.Heat);
            hash = Mix(hash, (uint)(int)t.Reactor.Health);
            hash = Mix(hash, (uint)t.Resources.Dirt);
            hash = Mix(hash, (uint)t.Resources.Minerals);
            hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(t.Heat));
            hash = Mix(hash, t.IsDead ? 1u : 0u);
        }

        var machines = world.Machines.Machines;
        for (int i = 0; i < machines.Count; i++)
        {
            var m = machines[i];
            hash = Mix(hash, (uint)m.Position.X);
            hash = Mix(hash, (uint)m.Position.Y);
            hash = Mix(hash, (uint)m.Type);
            hash = Mix(hash, (uint)m.State);
            hash = Mix(hash, (uint)m.OwnerColor);
            hash = Mix(hash, m.IsAlive ? 1u : 0u);
            hash = Mix(hash, (uint)(int)m.Reactor.Heat);
            hash = Mix(hash, (uint)(int)m.Reactor.Health);
        }

        var points = world.LinkMap.Points;
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            hash = Mix(hash, (uint)p.Id);
            hash = Mix(hash, (uint)p.Position.X);
            hash = Mix(hash, (uint)p.Position.Y);
            hash = Mix(hash, (uint)p.Type);
            hash = Mix(hash, p.IsEnabled ? 1u : 0u);
            hash = Mix(hash, p.IsPowered ? 1u : 0u);
        }

        var links = world.LinkMap.Links;
        for (int i = 0; i < links.Count; i++)
        {
            var l = links[i];
            hash = Mix(hash, (uint)l.From.Id);
            hash = Mix(hash, (uint)l.To.Id);
            hash = Mix(hash, (uint)l.Type);
            hash = Mix(hash, l.IsAlive ? 1u : 0u);
        }

        int w = world.Terrain.Width, h = world.Terrain.Height;
        var pix = new uint[w * h];
        var surface = new Surface(pix, w, h);
        world.LinkMap.Draw(surface);
        world.Machines.Draw(surface);
        world.Projectiles.Draw(surface);
        world.Sprites.Draw(surface);
        world.TankList.Draw(surface);
        for (int i = 0; i < pix.Length; i++)
            hash = Mix(hash, pix[i]);

        return hash;
    }
}
