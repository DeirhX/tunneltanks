namespace TunnelTanks.Core.Entities.Links;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Collision;
using TunnelTanks.Core.Terrain;

public enum LinkType { Live, Blocked, Theoretical }

public class Link
{
    public LinkPoint From { get; }
    public LinkPoint To { get; }
    public LinkType Type { get; set; } = LinkType.Theoretical;
    public bool IsAlive { get; set; } = true;

    public Link(LinkPoint from, LinkPoint to)
    {
        From = from;
        To = to;
    }

    public float Distance => VectorFExtensions.FromOffset(From.Position, To.Position).Length;

    public void UpdateType(Terrain terrain)
    {
        if (!From.IsEnabled || !To.IsEnabled || Distance > Tweaks.World.MaximumLiveLinkDistance)
        {
            Type = LinkType.Theoretical;
            return;
        }

        Type = IsPathBlocked(terrain) ? LinkType.Blocked : LinkType.Live;
    }

    private bool IsPathBlocked(Terrain terrain) =>
        Raycaster.BresenhamLineAny(From.Position, To.Position,
            pos => Pixel.IsBlockingCollision(terrain.GetPixel(pos)));
}

public static class VectorFExtensions
{
    public static VectorF FromOffset(Position a, Position b)
    {
        return new VectorF(b.X - a.X, b.Y - a.Y);
    }
}
