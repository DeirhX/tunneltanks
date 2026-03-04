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

    public LinkPoint(Position position, LinkPointType type, int id)
    {
        Position = position;
        Type = type;
        Id = id;
        IsPowered = type == LinkPointType.Base;
    }
}
