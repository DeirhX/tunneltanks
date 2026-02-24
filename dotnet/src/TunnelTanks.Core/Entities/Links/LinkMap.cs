namespace TunnelTanks.Core.Entities.Links;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Collision;
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

    public void Advance(TerrainGrid terrain)
    {
        if (_relinkTimer.Elapsed < _nextRelink) return;
        _nextRelink += Tweaks.World.RefreshLinkMapInterval;

        RebuildLinks(terrain);
    }

    private void RebuildLinks(TerrainGrid terrain)
    {
        _links.Clear();

        for (int i = 0; i < _points.Count; i++)
        {
            if (!_points[i].IsEnabled) continue;
            for (int j = i + 1; j < _points.Count; j++)
            {
                if (!_points[j].IsEnabled) continue;
                float dist = VectorF.FromPositions(_points[i].Position, _points[j].Position).Length;
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
        var liveColor = Tweaks.Colors.LinkLive.ToArgb();
        var blockedColor = Tweaks.Colors.LinkBlocked.ToArgb();

        foreach (var link in _links)
        {
            if (link.Type == LinkType.Theoretical) continue;

            uint color = link.Type == LinkType.Live ? liveColor : blockedColor;

            Raycaster.BresenhamLine(link.From.Position, link.To.Position, pos =>
            {
                if (pos.X >= 0 && pos.Y >= 0 && pos.X < surfaceWidth && pos.Y < surfaceHeight)
                    surface[pos.X + pos.Y * surfaceWidth] = color;
            });
        }
    }
}
