namespace Tunnerer.Core.Entities.Links;

using Tunnerer.Core.Types;

public enum LinkPointType { Base, Machine, Transit, Controllable }

public class LinkPoint
{
    public Position Position { get; set; }
    public LinkPointType Type { get; }
    public bool IsEnabled { get; set; } = true;
    public bool IsPowered { get; set; }
    public int Id { get; }

    private static int _nextId;

    public LinkPoint(Position position, LinkPointType type)
    {
        Position = position;
        Type = type;
        Id = Interlocked.Increment(ref _nextId);
        IsPowered = type == LinkPointType.Base;
    }
}
