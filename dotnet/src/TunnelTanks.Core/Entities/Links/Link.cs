namespace TunnelTanks.Core.Entities.Links;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
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
        float dist = Distance;
        if (dist > Tweaks.World.MaximumTheoreticalLinkDistance || !From.IsEnabled || !To.IsEnabled)
        {
            Type = LinkType.Theoretical;
            return;
        }

        if (dist > Tweaks.World.MaximumLiveLinkDistance)
        {
            Type = LinkType.Theoretical;
            return;
        }

        Type = IsPathBlocked(terrain) ? LinkType.Blocked : LinkType.Live;
    }

    private bool IsPathBlocked(Terrain terrain)
    {
        int x0 = From.Position.X, y0 = From.Position.Y;
        int x1 = To.Position.X, y1 = To.Position.Y;
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            var pix = terrain.GetPixel(new Position(x0, y0));
            if (Pixel.IsBlockingCollision(pix)) return true;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
        return false;
    }
}

public static class VectorFExtensions
{
    public static VectorF FromOffset(Position a, Position b)
    {
        return new VectorF(b.X - a.X, b.Y - a.Y);
    }
}
