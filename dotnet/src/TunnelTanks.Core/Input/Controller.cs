namespace TunnelTanks.Core.Input;

using TunnelTanks.Core.Types;

public struct ControllerOutput
{
    public Offset MoveSpeed;
    public bool ShootPrimary;
    public bool ShootSecondary;
    public DirectionF AimDirection;
    public bool BuildPrimary;
}
