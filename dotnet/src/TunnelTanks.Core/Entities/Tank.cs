namespace TunnelTanks.Core.Entities;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Resources;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Collision;
using TunnelTanks.Core.Input;
using TunnelTanks.Core.Rendering;
using TunnelTanks.Core.Entities.Projectiles;

public class Tank
{
    public Position Position { get; set; }
    public int Color { get; }
    public int Direction { get; set; }
    public Reactor Reactor { get; }
    public MaterialContainer Resources { get; }
    public int LivesLeft { get; set; }
    public bool IsDead => Reactor.Health <= 0 || Reactor.Energy <= 0;
    public TankBase? Base { get; }
    public TankTurret Turret { get; }

    private TimeSpan _respawnTimer;
    private bool _respawning;

    public Tank(int color, TankBase tankBase)
    {
        Color = color;
        Base = tankBase;
        Position = tankBase.Position;
        LivesLeft = Tweaks.Tank.MaxLives;
        Reactor = new Reactor(24000, 1000, 24000, 1000);
        Resources = new MaterialContainer(0, 0, 10000, 10000);
        Turret = new TankTurret(color);
        Direction = Random.Shared.Next(9);
        if (Direction >= 4) Direction++;
    }

    public void Advance(World world, ControllerOutput input)
    {
        if (!IsDead)
        {
            Reactor.Exhaust(new ReactorState(3, 0));

            var baseColl = world.TankBases.CheckBaseCollision(Position);
            if (baseColl != null)
            {
                baseColl.RechargeTank(Reactor, Color);
                if (baseColl.Color == Color)
                    baseColl.AbsorbResources(Resources, new MaterialAmount(15, 15));
            }

            // Set turret direction before movement (matches C++ order)
            if (input.AimDirection.X != 0 || input.AimDirection.Y != 0)
                Turret.SetDirection(input.AimDirection);
            else if (input.MoveSpeed.X != 0 || input.MoveSpeed.Y != 0)
            {
                float dx = input.MoveSpeed.X, dy = input.MoveSpeed.Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                Turret.SetDirection(new DirectionF(dx / len, dy / len));
            }

            Turret.Update(input.ShootPrimary);

            HandleMove(world.Terrain, input.MoveSpeed, Turret.Direction, input.ShootPrimary);

            if (input.ShootPrimary)
            {
                var bullet = Turret.TryShoot(Position, world.Projectiles);
                if (bullet != null)
                    Reactor.Exhaust(new ReactorState(160, 0));
            }

            CollectItems(world.Terrain);
        }
        else
        {
            AdvanceDeath(world);
        }
    }

    private void HandleMove(Terrain terrain, Offset speed, DirectionF torchHeading, bool torchUse)
    {
        if (speed.X == 0 && speed.Y == 0) return;

        int dir = SpeedToDirection(speed);
        var newPos = Position + speed;

        var collision = TestCollision(terrain, newPos, dir);
        if (collision != CollisionType.None)
        {
            DigTunnel(terrain, newPos, torchUse);

            // Check if torch (turret) is aimed in the same direction as movement
            int torchDir = SpeedToDirection(new Offset(
                (int)MathF.Round(torchHeading.X),
                (int)MathF.Round(torchHeading.Y)));

            if (!(torchUse && torchDir == dir))
                return; // Not shooting in movement direction → don't move on dig frame

            // Torch aligned: retest collision - may have failed to dig rock
            collision = TestCollision(terrain, newPos, dir);
            if (collision != CollisionType.None)
                return;
        }

        Direction = dir;
        Position = newPos;
        Reactor.Exhaust(new ReactorState(8, 0));
    }

    private CollisionType TestCollision(Terrain terrain, Position pos, int dir)
    {
        dir = Math.Clamp(dir, 0, TankSprites.DirectionCount - 1);
        var sprite = TankSprites.Sprites[dir];
        int w = TankSprites.SpriteWidth, h = TankSprites.SpriteHeight;
        int cx = w / 2, cy = h / 2;
        var result = CollisionType.None;

        for (int sy = 0; sy < h; sy++)
            for (int sx = 0; sx < w; sx++)
            {
                if (sprite[sx + sy * w] == 0) continue;
                var worldPos = new Position(pos.X - cx + sx, pos.Y - cy + sy);
                var pix = terrain.GetPixel(worldPos);
                if (Pixel.IsBlockingCollision(pix)) return CollisionType.Blocked;
                if (Pixel.IsSoftCollision(pix)) result = CollisionType.Dirt;
            }
        return result;
    }

    /// <summary>
    /// Digs a 7x7 area (minus corners) centered on the target position,
    /// matching the C++ DigTankTunnel behavior. Rock is only torchable
    /// (destroyed by random chance) when the player is actively shooting.
    /// </summary>
    private void DigTunnel(Terrain terrain, Position center, bool torchUse)
    {
        for (int ty = center.Y - 3; ty <= center.Y + 3; ty++)
            for (int tx = center.X - 3; tx <= center.X + 3; tx++)
            {
                if ((tx == center.X - 3 || tx == center.X + 3) &&
                    (ty == center.Y - 3 || ty == center.Y + 3))
                    continue;

                var worldPos = new Position(tx, ty);
                if (!terrain.IsInside(worldPos)) continue;

                var pix = terrain.GetPixelRaw(worldPos);
                if (Pixel.IsDiggable(pix))
                {
                    terrain.SetPixel(worldPos, TerrainPixel.Blank);
                    if (Pixel.IsDirt(pix))
                        Resources.Add(new MaterialAmount(1, 0));
                }
                else if (Pixel.IsTorchable(pix) && torchUse &&
                         Random.Shared.Next(1000) < Tweaks.World.DigThroughRockChance)
                {
                    terrain.SetPixel(worldPos, TerrainPixel.Blank);
                    if (Pixel.IsMineral(pix))
                        Resources.Add(new MaterialAmount(0, 1));
                }
            }
    }

    private void CollectItems(Terrain terrain)
    {
        int cx = TankSprites.SpriteWidth / 2, cy = TankSprites.SpriteHeight / 2;
        var sprite = TankSprites.Sprites[Math.Clamp(Direction, 0, TankSprites.DirectionCount - 1)];

        for (int sy = 0; sy < TankSprites.SpriteHeight; sy++)
            for (int sx = 0; sx < TankSprites.SpriteWidth; sx++)
            {
                if (sprite[sx + sy * TankSprites.SpriteWidth] == 0) continue;
                var worldPos = new Position(Position.X - cx + sx, Position.Y - cy + sy);
                if (!terrain.IsInside(worldPos)) continue;

                var pix = terrain.GetPixelRaw(worldPos);
                if (Pixel.IsEnergy(pix))
                {
                    terrain.SetPixel(worldPos, TerrainPixel.Blank);
                    int energyGain = pix switch
                    {
                        TerrainPixel.EnergyMedium => 200,
                        TerrainPixel.EnergyHigh => 400,
                        _ => 100,
                    };
                    Reactor.Add(new ReactorState(energyGain, 0));
                }
            }
    }

    public void Spawn()
    {
        if (Base == null) return;
        Reactor.Add(new ReactorState(Reactor.EnergyCapacity, Reactor.HealthCapacity));
        Position = Base.Position;
        _respawning = false;
    }

    public void Die(ProjectileList? projectiles = null)
    {
        Reactor.Exhaust(new ReactorState(Reactor.Energy, Reactor.Health));
        _respawning = true;
        _respawnTimer = Tweaks.Tank.RespawnDelay;

        projectiles?.AddRange(ExplosionFactory.CreateExplosion(
            Position, Tweaks.Explosion.Death.ShrapnelCount,
            Tweaks.Explosion.Death.Speed, Tweaks.Explosion.Death.Frames));
    }

    private void AdvanceDeath(World world)
    {
        if (!_respawning)
            Die(world.Projectiles);

        _respawnTimer -= Tweaks.World.AdvanceStep;
        if (_respawnTimer <= TimeSpan.Zero)
        {
            if (--LivesLeft > 0)
                Spawn();
        }
    }

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        if (IsDead) return;
        int dir = Math.Clamp(Direction, 0, TankSprites.DirectionCount - 1);
        var sprite = TankSprites.Sprites[dir];
        int w = TankSprites.SpriteWidth, h = TankSprites.SpriteHeight;
        int cx = w / 2, cy = h / 2;

        for (int sy = 0; sy < h; sy++)
            for (int sx = 0; sx < w; sx++)
            {
                byte val = sprite[sx + sy * w];
                if (val == 0) continue;
                int px = Position.X - cx + sx;
                int py = Position.Y - cy + sy;
                if (px < 0 || py < 0 || px >= surfaceWidth || py >= surfaceHeight) continue;
                surface[px + py * surfaceWidth] = TankSprites.GetPixelColor(val, Color).ToArgb();
            }

        Turret.Draw(surface, surfaceWidth, surfaceHeight, Position);
    }

    private static int SpeedToDirection(Offset speed)
    {
        // 8-direction mapping: 0=NE, 1=N, 2=NW, 3=E, 4=unused, 5=W, 6=SE, 7=S, 8=SW
        int dx = Math.Sign(speed.X), dy = Math.Sign(speed.Y);
        return (dx, dy) switch
        {
            ( 1, -1) => 0,
            ( 0, -1) => 1,
            (-1, -1) => 2,
            ( 1,  0) => 3,
            (-1,  0) => 5,
            ( 1,  1) => 6,
            ( 0,  1) => 7,
            (-1,  1) => 8,
            _ => 1,
        };
    }
}
