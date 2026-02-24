namespace TunnelTanks.Core.Entities;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Entities.Projectiles;

public class TankTurret
{
    private DirectionF _direction = new(0, -1);
    private int _cooldown;
    private readonly int _ownerColor;

    public DirectionF Direction => _direction;
    public bool IsShooting { get; private set; }

    public TankTurret(int ownerColor)
    {
        _ownerColor = ownerColor;
    }

    public void SetDirection(DirectionF dir) => _direction = dir;

    public void Update(bool shooting)
    {
        IsShooting = shooting;
        if (_cooldown > 0) _cooldown--;
    }

    public PositionF GetBarrelTip(Position tankPos)
    {
        return new PositionF(
            tankPos.X + _direction.X * Tweaks.Tank.TurretLength,
            tankPos.Y + _direction.Y * Tweaks.Tank.TurretLength);
    }

    public Projectile? TryShoot(Position tankPos, ProjectileList projectileList)
    {
        if (!IsShooting || _cooldown > 0) return null;

        _cooldown = Tweaks.Tank.TurretDelay;
        var barrel = GetBarrelTip(tankPos);
        var speed = _direction.ToVector(Tweaks.Weapon.CannonBulletSpeed);
        var bullet = Projectile.CreateBullet((Position)barrel, speed, _ownerColor);
        projectileList.Add(bullet);
        return bullet;
    }

    public void Reset()
    {
        _cooldown = 0;
    }

    public void Draw(Surface surface, Position tankPos)
    {
        var tip = GetBarrelTip(tankPos);
        int tx = (int)tip.X, ty = (int)tip.Y;
        if (surface.IsInside(tx, ty))
            surface.Pixels[tx + ty * surface.Width] = Tweaks.Colors.TurretBarrelTip.ToArgb();
    }
}
