namespace Tunnerer.Core.Input;

using Tunnerer.Core.Types;

public struct ControllerOutput
{
    public Offset MoveSpeed;
    public bool ShootPrimary;
    public bool ShootSecondary;
    public DirectionF AimDirection;
    public bool BuildPrimary;
}
