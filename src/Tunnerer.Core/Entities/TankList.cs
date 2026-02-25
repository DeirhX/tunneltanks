namespace Tunnerer.Core.Entities;

using Tunnerer.Core.Types;
using Tunnerer.Core.Input;
using Tunnerer.Core.Rendering;

public class TankList
{
    private readonly List<Tank> _tanks = new();

    public IReadOnlyList<Tank> Tanks => _tanks;

    public Tank AddTank(int color, TankBase tankBase)
    {
        var tank = new Tank(color, tankBase);
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
