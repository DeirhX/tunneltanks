namespace Tunnerer.Core.Entities.Links;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;
using Tunnerer.Core.Collision;
using Tunnerer.Core.Terrain;

public class LinkMap
{
    private readonly List<LinkPoint> _points = new();
    private readonly List<Link> _links = new();
    private readonly bool _deterministicSimulation;
    private readonly int _idSeed;
    private int _nextPointIndex;
    private TimeSpan _elapsed;
    private TimeSpan _nextRelink;

    public IReadOnlyList<LinkPoint> Points => _points;
    public IReadOnlyList<Link> Links => _links;

    public LinkMap(bool deterministicSimulation = false, int idSeed = 0)
    {
        _deterministicSimulation = deterministicSimulation;
        _idSeed = idSeed != 0 ? idSeed : 0x6E624EB7;
    }

    public LinkPoint RegisterPoint(Position position, LinkPointType type)
    {
        int id = _deterministicSimulation
            ? GenerateDeterministicId(_nextPointIndex++)
            : unchecked(_idSeed ^ ++_nextPointIndex);
        var point = new LinkPoint(position, type, id);
        _points.Add(point);
        return point;
    }

    public void RemoveAll()
    {
        _points.Clear();
        _links.Clear();
        _nextPointIndex = 0;
        _elapsed = TimeSpan.Zero;
        _nextRelink = TimeSpan.Zero;
    }

    public void Advance(TerrainGrid terrain, TimeSpan dt)
    {
        _elapsed += dt;
        if (_elapsed < _nextRelink) return;
        _nextRelink += Tweaks.World.RefreshLinkMapInterval;

        RebuildLinks(terrain);
    }

    private int GenerateDeterministicId(int pointIndex)
    {
        uint mixed = (uint)_idSeed ^ (uint)(pointIndex + 1) * 0x9e3779b9u;
        return unchecked((int)FastRandom.Hash32(mixed));
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

    public void Draw(Surface surface)
    {
        var liveColor = Tweaks.Colors.LinkLive.ToArgb();
        var blockedColor = Tweaks.Colors.LinkBlocked.ToArgb();

        foreach (var link in _links)
        {
            if (link.Type == LinkType.Theoretical) continue;

            uint color = link.Type == LinkType.Live ? liveColor : blockedColor;

            Raycaster.BresenhamLine(link.From.Position, link.To.Position, pos =>
            {
                if (surface.IsInside(pos.X, pos.Y))
                    surface.Pixels[pos.X + pos.Y * surface.Width] = color;
            });
        }
    }
}
