namespace TunnelTanks.Core.Entities.Machines;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Resources;
using TunnelTanks.Core.Terrain;

public enum MachineType { Harvester, Charger }
public enum MachineState { Template, Transporting, Planted }

public class Machine
{
    public Position Position { get; set; }
    public MachineType Type { get; }
    public MachineState State { get; set; } = MachineState.Template;
    public Reactor Reactor { get; }
    public int OwnerColor { get; }
    public BoundingBox BoundingBox { get; } = new(Tweaks.Machine.BoundingBoxHalfSize, Tweaks.Machine.BoundingBoxHalfSize);
    public bool IsAlive { get; set; } = true;

    private TimeSpan _actionAccumulator;
    private readonly TimeSpan _actionInterval;

    public Machine(Position position, MachineType type, int ownerColor)
    {
        Position = position;
        Type = type;
        OwnerColor = ownerColor;
        Reactor = new Reactor(0, Tweaks.Machine.ReactorHealthCapacity,
            Tweaks.Machine.ReactorEnergyCapacity, Tweaks.Machine.ReactorHealthCapacity);
        _actionInterval = type == MachineType.Harvester
            ? TimeSpan.FromMilliseconds(Tweaks.Machine.HarvesterIntervalMs)
            : TimeSpan.FromMilliseconds(Tweaks.Machine.ChargerIntervalMs);
    }

    public bool IsBlockingCollision => State == MachineState.Planted;
    public bool TestCollide(Position pos) => BoundingBox.IsInside(pos, Position);

    public void Advance(TerrainGrid terrain, TimeSpan dt)
    {
        if (!IsAlive || State != MachineState.Planted) return;
        if (Reactor.Health <= 0) { IsAlive = false; return; }

        _actionAccumulator += dt;
        if (_actionAccumulator < _actionInterval) return;
        _actionAccumulator -= _actionInterval;

        if (Type == MachineType.Harvester)
            HarvestNearby(terrain);
    }

    private void HarvestNearby(TerrainGrid terrain)
    {
        int range = Tweaks.Machine.HarvestRange;
        for (int dy = -range; dy <= range; dy++)
            for (int dx = -range; dx <= range; dx++)
            {
                if (dx * dx + dy * dy > range * range) continue;
                var pos = new Position(Position.X + dx, Position.Y + dy);
                if (!terrain.IsInside(pos)) continue;

                var pix = terrain.GetPixelRaw(pos);
                if (Pixel.IsDirt(pix))
                {
                    terrain.SetPixel(pos, TerrainPixel.Blank);
                    return;
                }
            }
    }

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        if (!IsAlive) return;
        int half = BoundingBox.HalfWidth;
        uint color = Type == MachineType.Harvester
            ? Tweaks.Colors.Harvester.ToArgb()
            : Tweaks.Colors.Charger.ToArgb();

        if (State == MachineState.Template)
            color = Tweaks.Colors.MachineTemplate.ToArgb();

        for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++)
            {
                int px = Position.X + dx, py = Position.Y + dy;
                if (px < 0 || py < 0 || px >= surfaceWidth || py >= surfaceHeight) continue;
                bool edge = Math.Abs(dx) == half || Math.Abs(dy) == half;
                if (edge)
                    surface[px + py * surfaceWidth] = color;
            }
    }
}
