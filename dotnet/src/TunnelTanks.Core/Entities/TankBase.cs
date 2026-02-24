namespace TunnelTanks.Core.Entities;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Resources;
using TunnelTanks.Core.Terrain;

public class TankBase
{
    public Position Position { get; }
    public int Color { get; }
    public BoundingBox BoundingBox { get; } = new(Tweaks.Base.BaseSize / 2, Tweaks.Base.BaseSize / 2);
    public Reactor Reactor { get; } = new(15000, 2000, 30000, 2000);
    public MaterialContainer Materials { get; } = new(0, 0, 2000, 20000);

    public TankBase(Position position, int color)
    {
        Position = position;
        Color = color;
    }

    public bool IsInside(Position tested) => BoundingBox.IsInside(tested, Position);

    public void Advance()
    {
        Reactor.Add(new ReactorState(100, 10));
    }

    public void RechargeTank(Reactor tankReactor, int tankColor)
    {
        if (tankColor == Color)
            GiveReactorResources(tankReactor, new ReactorState(300, 3));
        else
            GiveReactorResources(tankReactor, new ReactorState(90, 1));
    }

    private void GiveReactorResources(Reactor target, ReactorState rate)
    {
        var absorber = new Reactor(0, 0, rate.Energy, rate.Health);
        absorber.Absorb(Reactor);
        target.Absorb(absorber);
        Reactor.Absorb(absorber);
    }

    public void AbsorbResources(MaterialContainer other)
    {
        Materials.Absorb(other);
    }

    public void AbsorbResources(MaterialContainer other, MaterialAmount rate)
    {
        var absorber = new MaterialContainer(0, 0, rate.Dirt, rate.Minerals);
        absorber.Absorb(other);
        Materials.Absorb(absorber);
        other.Absorb(absorber);
    }

    public void Draw(uint[] surface, int surfaceWidth)
    {
        int halfW = BoundingBox.HalfWidth;
        int halfH = BoundingBox.HalfHeight;
        var energyColor = new Color(60, 200, 60).ToArgb();
        var outlineColor = new Color(40, 40, 40).ToArgb();

        for (int dy = -halfH; dy <= halfH; dy++)
            for (int dx = -halfW; dx <= halfW; dx++)
            {
                int px = Position.X + dx, py = Position.Y + dy;
                if (px < 0 || py < 0 || px >= surfaceWidth) continue;
                bool isEdge = Math.Abs(dx) == halfW || Math.Abs(dy) == halfH;
                if (isEdge)
                {
                    bool isDoor = dx >= -Tweaks.Base.DoorSize / 2 && dx <= Tweaks.Base.DoorSize / 2;
                    if (!isDoor)
                        surface[px + py * surfaceWidth] = outlineColor;
                }
            }
    }
}

public class TankBases
{
    private readonly List<TankBase> _bases = new();

    public IReadOnlyList<TankBase> Bases => _bases;

    public void AddBase(Position position, int color)
    {
        _bases.Add(new TankBase(position, color));
    }

    public TankBase? GetSpawn(int color)
    {
        return color >= 0 && color < _bases.Count ? _bases[color] : null;
    }

    public TankBase? CheckBaseCollision(Position pos)
    {
        foreach (var b in _bases)
            if (b.IsInside(pos))
                return b;
        return null;
    }

    public void CreateBasesInTerrain(Terrain terrain)
    {
        for (int i = 0; i < _bases.Count; i++)
            CreateBaseInTerrain(_bases[i].Position, i, terrain);
    }

    private void CreateBaseInTerrain(Position pos, int color, Terrain terrain)
    {
        int half = Tweaks.Base.BaseSize / 2;
        int doorHalf = Tweaks.Base.DoorSize / 2;

        for (int y = -half; y <= half; y++)
            for (int x = -half; x <= half; x++)
            {
                var pix = pos + new Offset(x, y);
                if (!terrain.IsInside(pix)) continue;

                if (Math.Abs(x) == half || Math.Abs(y) == half)
                {
                    if (x >= -doorHalf && x <= doorHalf)
                        terrain.SetPixel(pix, TerrainPixel.BaseBarrier);
                    else
                        terrain.SetPixel(pix, (TerrainPixel)((byte)TerrainPixel.BaseMin + color));
                }
                else
                {
                    terrain.SetPixel(pix, TerrainPixel.Blank);
                }
            }
    }

    public void Advance()
    {
        foreach (var b in _bases)
            b.Advance();
    }
}
