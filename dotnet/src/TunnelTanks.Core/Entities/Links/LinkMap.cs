namespace TunnelTanks.Core.Entities.Links;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using System.Diagnostics;
using TunnelTanks.Core.Terrain;

public class LinkMap
{
    private readonly List<LinkPoint> _points = new();
    private readonly List<Link> _links = new();
    private readonly Stopwatch _relinkTimer = Stopwatch.StartNew();
    private TimeSpan _nextRelink;

    public IReadOnlyList<LinkPoint> Points => _points;
    public IReadOnlyList<Link> Links => _links;

    public LinkPoint RegisterPoint(Position position, LinkPointType type)
    {
        var point = new LinkPoint(position, type);
        _points.Add(point);
        return point;
    }

    public void RemoveAll()
    {
        _points.Clear();
        _links.Clear();
    }

    public void Advance(Terrain terrain)
    {
        if (_relinkTimer.Elapsed < _nextRelink) return;
        _nextRelink += Tweaks.World.RefreshLinkMapInterval;

        RebuildLinks(terrain);
    }

    private void RebuildLinks(Terrain terrain)
    {
        _links.Clear();

        for (int i = 0; i < _points.Count; i++)
        {
            if (!_points[i].IsEnabled) continue;
            for (int j = i + 1; j < _points.Count; j++)
            {
                if (!_points[j].IsEnabled) continue;
                float dist = VectorFExtensions.FromOffset(_points[i].Position, _points[j].Position).Length;
                if (dist > Tweaks.World.MaximumTheoreticalLinkDistance) continue;

                var link = new Link(_points[i], _points[j]);
                link.UpdateType(terrain);
                _links.Add(link);
            }
        }

        // Propagate power from bases
        foreach (var p in _points)
            p.IsPowered = p.Type == LinkPointType.Base;

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var link in _links)
            {
                if (link.Type != LinkType.Live) continue;
                if (link.From.IsPowered && !link.To.IsPowered) { link.To.IsPowered = true; changed = true; }
                if (link.To.IsPowered && !link.From.IsPowered) { link.From.IsPowered = true; changed = true; }
            }
        }
    }

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        var liveColor = new Color(0xa0, 0xa0, 0x19).ToArgb();
        var blockedColor = new Color(0xa0, 0x30, 0x30).ToArgb();

        foreach (var link in _links)
        {
            if (link.Type == LinkType.Theoretical) continue;

            uint color = link.Type == LinkType.Live ? liveColor : blockedColor;

            int x0 = link.From.Position.X, y0 = link.From.Position.Y;
            int x1 = link.To.Position.X, y1 = link.To.Position.Y;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                if (x0 >= 0 && y0 >= 0 && x0 < surfaceWidth && y0 < surfaceHeight)
                    surface[x0 + y0 * surfaceWidth] = color;
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }
}
