namespace Tunnerer.Core.Input;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Collision;

public enum TwitchMode { Start, ExitBaseUp, ExitBaseDown, Twitch, Return, Recharge }

public class BotTankAI
{
    private readonly record struct BehaviorProfile(
        string Name,
        float LowHealthRatio,
        float HighHeatRatio,
        int OutgunnedHealthGap,
        int ShootChanceLosPermille,
        int ShootChanceDigPermille,
        int ReplanLosMin,
        int ReplanLosMax,
        int ReplanTunnelMin,
        int ReplanTunnelMax,
        float DistanceWeight,
        float OpenTileBonus,
        float SoftTileBonus,
        float HardTileBonus);

    private static readonly int OutsideBase = Tweaks.Base.BaseSize / 2 + 5;
    private static readonly BehaviorProfile[] Profiles =
    {
        new(
            Name: "cautious",
            LowHealthRatio: 0.70f,
            HighHeatRatio: 0.55f,
            OutgunnedHealthGap: 40,
            ShootChanceLosPermille: 500,
            ShootChanceDigPermille: 250,
            ReplanLosMin: 5,
            ReplanLosMax: 9,
            ReplanTunnelMin: 7,
            ReplanTunnelMax: 14,
            DistanceWeight: 0.012f,
            OpenTileBonus: 46f,
            SoftTileBonus: 10f,
            HardTileBonus: -4f),
        new(
            Name: "balanced",
            LowHealthRatio: 0.50f,
            HighHeatRatio: 0.67f,
            OutgunnedHealthGap: 100,
            ShootChanceLosPermille: 700,
            ShootChanceDigPermille: 450,
            ReplanLosMin: 6,
            ReplanLosMax: 12,
            ReplanTunnelMin: 8,
            ReplanTunnelMax: 20,
            DistanceWeight: 0.015f,
            OpenTileBonus: 36f,
            SoftTileBonus: 12f,
            HardTileBonus: 2f),
        new(
            Name: "rush",
            LowHealthRatio: 0.30f,
            HighHeatRatio: 0.80f,
            OutgunnedHealthGap: 220,
            ShootChanceLosPermille: 900,
            ShootChanceDigPermille: 700,
            ReplanLosMin: 4,
            ReplanLosMax: 8,
            ReplanTunnelMin: 6,
            ReplanTunnelMax: 12,
            DistanceWeight: 0.020f,
            OpenTileBonus: 26f,
            SoftTileBonus: 16f,
            HardTileBonus: 10f),
    };
    private const int ProfileSwitchSeconds = 4;
    private const int ExitStuckThresholdFrames = 10;
    private const int GeneralStuckThresholdFrames = 7;
    private const int EvasiveSwitchMinFrames = 5;
    private const int EvasiveSwitchMaxFrames = 11;
    private static readonly Offset[] MoveCandidates =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    };

    private TwitchMode _mode = TwitchMode.Start;
    private int _dx, _dy;
    private bool _shoot;
    private int _timeToChange;
    private DirectionF _aimDirection;
    private readonly Random _rng;
    private int _activeProfileIdx;
    private int _framesUntilProfileSwitch = ProfileSwitchSeconds * Tweaks.Perf.TargetFps;
    private Position _lastPosition;
    private bool _hasLastPosition;
    private int _stuckFrames;
    private int _evasiveDir = 1;
    private int _evasiveFramesRemaining;

    public BotTankAI(int? seed = null) { _rng = seed.HasValue ? new Random(seed.Value) : new Random(); }

    public ControllerOutput GetInput(Tank tank, Tank? enemy, TerrainGrid terrain)
    {
        AdvanceProfileCycle();
        UpdateStuckState(tank.Position);

        var basePos = tank.Base?.Position ?? tank.Position;
        var relX = tank.Position.X - basePos.X;
        var relY = tank.Position.Y - basePos.Y;

        return _mode switch
        {
            TwitchMode.Start => DoStart(),
            TwitchMode.ExitBaseUp => DoExitUp(tank, terrain, relY),
            TwitchMode.ExitBaseDown => DoExitDown(tank, terrain, relY),
            TwitchMode.Twitch => DoTwitch(tank, enemy, terrain, relX, relY),
            TwitchMode.Return => DoReturn(relX, relY),
            TwitchMode.Recharge => DoRecharge(tank, relX, relY),
            _ => default,
        };
    }

    private ControllerOutput DoStart()
    {
        _mode = _rng.Next(2) == 0 ? TwitchMode.ExitBaseUp : TwitchMode.ExitBaseDown;
        return default;
    }

    private ControllerOutput DoExitUp(Tank tank, TerrainGrid terrain, int relY)
    {
        if (relY < -OutsideBase) { _timeToChange = 0; _mode = TwitchMode.Twitch; return default; }
        if (_stuckFrames >= ExitStuckThresholdFrames)
        {
            _mode = TwitchMode.ExitBaseDown;
            return BuildUnstuckOutput(tank.Position, terrain, preferY: -1);
        }

        var move = new Offset(0, -1);
        return new ControllerOutput
        {
            MoveSpeed = move,
            ShootPrimary = NeedsDigging(tank.Position, move, terrain),
            AimDirection = AimTowardsOffset(move),
        };
    }

    private ControllerOutput DoExitDown(Tank tank, TerrainGrid terrain, int relY)
    {
        if (relY > OutsideBase) { _timeToChange = 0; _mode = TwitchMode.Twitch; return default; }
        if (_stuckFrames >= ExitStuckThresholdFrames)
        {
            _mode = TwitchMode.ExitBaseUp;
            return BuildUnstuckOutput(tank.Position, terrain, preferY: 1);
        }

        var move = new Offset(0, 1);
        return new ControllerOutput
        {
            MoveSpeed = move,
            ShootPrimary = NeedsDigging(tank.Position, move, terrain),
            AimDirection = AimTowardsOffset(move),
        };
    }

    private ControllerOutput DoTwitch(Tank tank, Tank? enemy, TerrainGrid terrain, int relX, int relY)
    {
        var profile = ActiveProfile;
        if (tank.Reactor.Health < 500 || tank.Reactor.Heat > 70 ||
            (Math.Abs(relX) < Tweaks.Base.BaseSize / 2 && Math.Abs(relY) < Tweaks.Base.BaseSize / 2))
        {
            _mode = TwitchMode.Return;
        }

        if (_timeToChange <= 0)
        {
            bool hasTarget = enemy != null && !enemy.IsDead;
            bool hasLos = hasTarget && HasLineOfSight(tank.Position, enemy!.Position, terrain);
            bool retreat = hasLos && ShouldRetreat(tank, enemy!, profile);
            var targetPos = hasTarget ? enemy!.Position : (Position?)null;

            Offset desiredMove = hasLos
                ? ChooseCombatMove(tank.Position, enemy!.Position, terrain, retreat)
                : ChooseTunnelMove(tank.Position, targetPos, terrain);

            _dx = desiredMove.X;
            _dy = desiredMove.Y;
            if (_dx == 0 && _dy == 0)
            {
                var fallback = MoveCandidates[_rng.Next(MoveCandidates.Length)];
                _dx = fallback.X;
                _dy = fallback.Y;
            }

            bool shouldDig = NeedsDigging(tank.Position, new Offset(_dx, _dy), terrain);
            _shoot = retreat && hasLos
                ? _rng.Next(1000) < Math.Max(profile.ShootChanceLosPermille, 820)
                : hasLos
                    ? _rng.Next(1000) < profile.ShootChanceLosPermille
                    : shouldDig && _rng.Next(1000) < profile.ShootChanceDigPermille;
            _aimDirection = hasTarget ? AimTowards(tank.Position, enemy!.Position) : AimTowardsOffset(new Offset(_dx, _dy));
            _timeToChange = hasLos
                ? _rng.Next(profile.ReplanLosMin, profile.ReplanLosMax)
                : _rng.Next(profile.ReplanTunnelMin, profile.ReplanTunnelMax);

            if (_stuckFrames >= GeneralStuckThresholdFrames)
            {
                var unstuck = ChooseUnstuckMove(tank.Position, terrain);
                _dx = unstuck.X;
                _dy = unstuck.Y;
                _shoot = NeedsDigging(tank.Position, unstuck, terrain);
                _aimDirection = AimTowardsOffset(unstuck);
                _timeToChange = _rng.Next(4, 8);
            }
        }

        _timeToChange--;
        return new ControllerOutput
        {
            MoveSpeed = new Offset(_dx, _dy),
            ShootPrimary = _shoot,
            AimDirection = _aimDirection,
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
        if (tank.Reactor.Health >= Tweaks.Tank.InitialHealth &&
            tank.Reactor.Heat <= (int)(Tweaks.Tank.HeatCapacity * 0.1f))
        {
            _mode = TwitchMode.Start;
            return default;
        }

        int sx = relX != 0 ? (relX < 0 ? 1 : -1) : 0;
        int sy = relY != 0 ? (relY < 0 ? 1 : -1) : 0;
        return new ControllerOutput { MoveSpeed = new Offset(sx, sy) };
    }

    private bool ShouldRetreat(Tank self, Tank enemy, BehaviorProfile profile)
    {
        bool lowHealth = self.Reactor.Health < (int)(Tweaks.Tank.InitialHealth * profile.LowHealthRatio);
        bool highHeat = self.Reactor.Heat > (int)(Tweaks.Tank.HeatCapacity * profile.HighHeatRatio);
        bool outgunned = self.Reactor.Health + profile.OutgunnedHealthGap < enemy.Reactor.Health;
        return lowHealth || highHeat || outgunned;
    }

    private Offset ChooseCombatMove(Position self, Position enemy, TerrainGrid terrain, bool retreat)
    {
        if (retreat)
            return ChooseRetreatEvasiveMove(self, enemy, terrain);

        Position target = retreat
            ? new Position(self.X + (self.X - enemy.X) * 3, self.Y + (self.Y - enemy.Y) * 3)
            : enemy;
        return ChooseBestMove(self, target, terrain);
    }

    private Offset ChooseTunnelMove(Position self, Position? target, TerrainGrid terrain)
    {
        return ChooseBestMove(self, target, terrain);
    }

    private Offset ChooseBestMove(Position self, Position? target, TerrainGrid terrain)
    {
        var previous = new Offset(_dx, _dy);
        float bestScore = float.NegativeInfinity;
        Offset best = default;

        foreach (var candidate in MoveCandidates)
        {
            float score = ScoreMove(self, candidate, target, terrain, previous);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private float ScoreMove(Position self, Offset move, Position? target, TerrainGrid terrain, Offset previous)
    {
        var profile = ActiveProfile;
        var next = self + move;
        if (!terrain.IsInside(next))
            return float.NegativeInfinity;

        float score = 0f;
        var nextPix = terrain.GetPixelRaw(next);
        bool anyCollision = Pixel.IsAnyCollision(nextPix);
        bool hardCollision = Pixel.IsBlockingCollision(nextPix);

        if (!anyCollision)
            score += profile.OpenTileBonus; // Prefer already dug/open tunnels for better throughput.
        else if (!hardCollision)
            score += profile.SoftTileBonus; // Soft collision is still acceptable (dirt can be carved).
        else
            score += profile.HardTileBonus; // Hard collision is mostly for deliberate digging profiles.

        for (int step = 2; step <= 4; step++)
        {
            var probe = new Position(self.X + move.X * step, self.Y + move.Y * step);
            if (!terrain.IsInside(probe))
            {
                score -= 8f;
                break;
            }

            var pix = terrain.GetPixelRaw(probe);
            if (Pixel.IsBlockingCollision(pix))
            {
                score -= step == 2 ? 14f : 8f;
                break;
            }

            score += Pixel.IsAnyCollision(pix) ? -1.5f : 4f;
        }

        if (target.HasValue)
        {
            int oldDist = Position.DistanceSquared(self, target.Value);
            int newDist = Position.DistanceSquared(next, target.Value);
            score += (oldDist - newDist) * profile.DistanceWeight;
        }

        if (move.X == previous.X && move.Y == previous.Y)
            score += 2f;
        else if (move.X == -previous.X && move.Y == -previous.Y)
            score -= 3f;

        score += (float)_rng.NextDouble();
        return score;
    }

    private bool NeedsDigging(Position self, Offset move, TerrainGrid terrain)
    {
        var next = self + move;
        if (!terrain.IsInside(next))
            return false;
        return Pixel.IsAnyCollision(terrain.GetPixelRaw(next));
    }

    private bool HasLineOfSight(Position from, Position to, TerrainGrid terrain)
    {
        bool blocked = Raycaster.BresenhamLineAny(from, to, pos =>
        {
            if (pos == from || pos == to)
                return false;
            if (!terrain.IsInside(pos))
                return true;
            return Pixel.IsBlockingCollision(terrain.GetPixelRaw(pos));
        });
        return !blocked;
    }

    private static DirectionF AimTowards(Position from, Position to)
    {
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f)
            return default;
        return new DirectionF(dx / len, dy / len);
    }

    private static DirectionF AimTowardsOffset(Offset offset)
    {
        float dx = offset.X;
        float dy = offset.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f)
            return default;
        return new DirectionF(dx / len, dy / len);
    }

    private void UpdateStuckState(Position current)
    {
        if (!_hasLastPosition)
        {
            _lastPosition = current;
            _hasLastPosition = true;
            _stuckFrames = 0;
            return;
        }

        if (current == _lastPosition)
            _stuckFrames++;
        else
            _stuckFrames = 0;

        _lastPosition = current;
    }

    private ControllerOutput BuildUnstuckOutput(Position self, TerrainGrid terrain, int preferY)
    {
        var move = ChooseUnstuckMove(self, terrain, preferY);
        return new ControllerOutput
        {
            MoveSpeed = move,
            ShootPrimary = NeedsDigging(self, move, terrain),
            AimDirection = AimTowardsOffset(move),
        };
    }

    private Offset ChooseUnstuckMove(Position self, TerrainGrid terrain, int preferredYDirection = 0)
    {
        float bestScore = float.NegativeInfinity;
        Offset best = MoveCandidates[_rng.Next(MoveCandidates.Length)];

        foreach (var candidate in MoveCandidates)
        {
            var next = self + candidate;
            if (!terrain.IsInside(next))
                continue;

            float score = 0f;
            var pix = terrain.GetPixelRaw(next);
            if (!Pixel.IsAnyCollision(pix))
                score += 20f;
            else if (!Pixel.IsBlockingCollision(pix))
                score += 8f;

            if (preferredYDirection != 0)
                score += candidate.Y == preferredYDirection ? 6f : 0f;

            score += (float)_rng.NextDouble();
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private Offset ChooseRetreatEvasiveMove(Position self, Position enemy, TerrainGrid terrain)
    {
        int awayX = Math.Sign(self.X - enemy.X);
        int awayY = Math.Sign(self.Y - enemy.Y);
        var away = new Offset(awayX, awayY);

        if (_evasiveFramesRemaining <= 0)
        {
            _evasiveDir = _rng.Next(2) == 0 ? -1 : 1;
            _evasiveFramesRemaining = _rng.Next(EvasiveSwitchMinFrames, EvasiveSwitchMaxFrames);
        }
        _evasiveFramesRemaining--;

        var leftPerp = new Offset(-away.Y, away.X);
        var strafe = _evasiveDir > 0 ? leftPerp : new Offset(-leftPerp.X, -leftPerp.Y);

        var composite = new Offset(
            Math.Clamp(away.X + strafe.X, -1, 1),
            Math.Clamp(away.Y + strafe.Y, -1, 1));

        if (composite.X == 0 && composite.Y == 0)
            composite = away.X != 0 || away.Y != 0 ? away : strafe;

        return ChooseBestMove(self, self + composite * 6, terrain);
    }

    private BehaviorProfile ActiveProfile => Profiles[_activeProfileIdx];

    private void AdvanceProfileCycle()
    {
        _framesUntilProfileSwitch--;
        if (_framesUntilProfileSwitch > 0)
            return;

        _activeProfileIdx = (_activeProfileIdx + 1) % Profiles.Length;
        _framesUntilProfileSwitch = ProfileSwitchSeconds * Tweaks.Perf.TargetFps;
    }
}
