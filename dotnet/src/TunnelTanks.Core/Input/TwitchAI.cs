namespace TunnelTanks.Core.Input;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Entities;

public enum TwitchMode { Start, ExitBaseUp, ExitBaseDown, Twitch, Return, Recharge }

public class TwitchAI
{
    private static readonly int OutsideBase = Tweaks.Base.BaseSize / 2 + 5;

    private TwitchMode _mode = TwitchMode.Start;
    private int _dx, _dy;
    private bool _shoot;
    private int _timeToChange;
    private readonly Random _rng;

    public TwitchAI(int? seed = null) { _rng = seed.HasValue ? new Random(seed.Value) : new Random(); }

    public ControllerOutput GetInput(Tank tank)
    {
        var basePos = tank.Base?.Position ?? tank.Position;
        var relX = tank.Position.X - basePos.X;
        var relY = tank.Position.Y - basePos.Y;

        return _mode switch
        {
            TwitchMode.Start => DoStart(relY),
            TwitchMode.ExitBaseUp => DoExitUp(relY),
            TwitchMode.ExitBaseDown => DoExitDown(relY),
            TwitchMode.Twitch => DoTwitch(tank, relX, relY),
            TwitchMode.Return => DoReturn(relX, relY),
            TwitchMode.Recharge => DoRecharge(tank, relX, relY),
            _ => default,
        };
    }

    private ControllerOutput DoStart(int relY)
    {
        _mode = _rng.Next(2) == 0 ? TwitchMode.ExitBaseUp : TwitchMode.ExitBaseDown;
        return default;
    }

    private ControllerOutput DoExitUp(int relY)
    {
        if (relY < -OutsideBase) { _timeToChange = 0; _mode = TwitchMode.Twitch; return default; }
        return new ControllerOutput { MoveSpeed = new Offset(0, -1) };
    }

    private ControllerOutput DoExitDown(int relY)
    {
        if (relY > OutsideBase) { _timeToChange = 0; _mode = TwitchMode.Twitch; return default; }
        return new ControllerOutput { MoveSpeed = new Offset(0, 1) };
    }

    private ControllerOutput DoTwitch(Tank tank, int relX, int relY)
    {
        if (tank.Reactor.Health < 500 || tank.Reactor.Energy < 8000 ||
            (Math.Abs(relX) < Tweaks.Base.BaseSize / 2 && Math.Abs(relY) < Tweaks.Base.BaseSize / 2))
        {
            _mode = TwitchMode.Return;
        }

        if (_timeToChange <= 0)
        {
            _timeToChange = _rng.Next(10, 30);
            _dx = _rng.Next(3) - 1;
            _dy = _rng.Next(3) - 1;
            _shoot = _rng.Next(1000) < 300;
        }

        _timeToChange--;
        return new ControllerOutput
        {
            MoveSpeed = new Offset(_dx, _dy),
            ShootPrimary = _shoot,
        };
    }

    private ControllerOutput DoReturn(int relX, int relY)
    {
        int targetY = relY < 0 ? -OutsideBase : OutsideBase;

        if ((relX == 0 && relY == targetY) ||
            (Math.Abs(relX) < Tweaks.Base.BaseSize / 2 && Math.Abs(relY) < Tweaks.Base.BaseSize / 2))
        {
            _mode = TwitchMode.Recharge;
            return default;
        }

        if (Math.Abs(relX) <= OutsideBase && Math.Abs(relY) < OutsideBase)
            return new ControllerOutput { MoveSpeed = new Offset(0, relY < targetY ? 1 : -1) };

        int sx = relX != 0 ? (relX < 0 ? 1 : -1) : 0;
        int sy = relY != targetY ? (relY < targetY ? 1 : -1) : 0;
        return new ControllerOutput { MoveSpeed = new Offset(sx, sy) };
    }

    private ControllerOutput DoRecharge(Tank tank, int relX, int relY)
    {
        if (tank.Reactor.Health >= 1000 && tank.Reactor.Energy >= 24000)
        {
            _mode = TwitchMode.Start;
            return default;
        }

        int sx = relX != 0 ? (relX < 0 ? 1 : -1) : 0;
        int sy = relY != 0 ? (relY < 0 ? 1 : -1) : 0;
        return new ControllerOutput { MoveSpeed = new Offset(sx, sy) };
    }
}
