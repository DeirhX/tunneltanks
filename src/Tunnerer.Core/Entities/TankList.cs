namespace Tunnerer.Core.Entities;

using Tunnerer.Core.Types;
using Tunnerer.Core.Input;
using Tunnerer.Core.Rendering;

public class TankList
{
    private readonly List<Tank> _tanks = new();

    public IReadOnlyList<Tank> Tanks => _tanks;
    public int Count => _tanks.Count;

    public Tank AddTank(int color, TankBase tankBase, int rngSeed = 0)
    {
        var tank = new Tank(color, tankBase, rngSeed);
        _tanks.Add(tank);
        return tank;
    }

    public void Advance(World world, Func<int, ControllerOutput> getInput)
    {
        for (int i = 0; i < _tanks.Count; i++)
            _tanks[i].Advance(world, getInput(i));
    }

    public void Draw(Surface surface)
    {
        foreach (var tank in _tanks)
            tank.Draw(surface);
    }

    public int CopyRenderStates(Span<TankRenderState> destination)
    {
        int count = Math.Min(destination.Length, _tanks.Count);
        for (int i = 0; i < count; i++)
        {
            var tank = _tanks[i];
            destination[i] = new TankRenderState(
                Position: tank.Position,
                Color: tank.Color,
                Direction: tank.Direction,
                IsDead: tank.IsDead,
                TurretDirection: tank.Turret.Direction);
        }
        return count;
    }

    public Tank? CheckTankCollision(Position position, int excludeColor)
    {
        foreach (var tank in _tanks)
        {
            if (tank.IsDead || tank.Color == excludeColor) continue;
            int cx = TankSprites.SpriteWidth / 2;
            int cy = TankSprites.SpriteHeight / 2;
            if (Math.Abs(position.X - tank.Position.X) <= cx &&
                Math.Abs(position.Y - tank.Position.Y) <= cy)
                return tank;
        }
        return null;
    }
}
