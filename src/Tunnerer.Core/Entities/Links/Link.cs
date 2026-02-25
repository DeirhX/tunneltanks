namespace Tunnerer.Core.Entities.Links;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;
using Tunnerer.Core.Collision;
using Tunnerer.Core.Terrain;

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

    public float Distance => VectorF.FromPositions(From.Position, To.Position).Length;

    public void UpdateType(TerrainGrid terrain)
    {
        if (!From.IsEnabled || !To.IsEnabled || Distance > Tweaks.World.MaximumLiveLinkDistance)
        {
            Type = LinkType.Theoretical;
            return;
        }

        Type = IsPathBlocked(terrain) ? LinkType.Blocked : LinkType.Live;
    }

    private bool IsPathBlocked(TerrainGrid terrain) =>
        Raycaster.BresenhamLineAny(From.Position, To.Position,
            pos => Pixel.IsBlockingCollision(terrain.GetPixel(pos)));
}
