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
    public MachineBehavior Behavior { get; }
    public MachineState State { get; set; } = MachineState.Template;
    public Reactor Reactor { get; }
    public int OwnerColor { get; }
    public BoundingBox BoundingBox { get; } = new(Tweaks.Machine.BoundingBoxHalfSize, Tweaks.Machine.BoundingBoxHalfSize);
    public bool IsAlive { get; set; } = true;

    private TimeSpan _actionAccumulator;

    public Machine(Position position, MachineType type, int ownerColor)
    {
        Position = position;
        Type = type;
        Behavior = MachineBehaviors.Get(type);
        OwnerColor = ownerColor;
        Reactor = new Reactor(0, Tweaks.Machine.ReactorHealthCapacity,
            Tweaks.Machine.ReactorEnergyCapacity, Tweaks.Machine.ReactorHealthCapacity);
    }

    public bool IsBlockingCollision => State == MachineState.Planted;
    public bool TestCollide(Position pos) => BoundingBox.IsInside(pos, Position);

    public void Advance(TerrainGrid terrain, TimeSpan dt)
    {
        if (!IsAlive || State != MachineState.Planted) return;
        if (Reactor.Health <= 0) { IsAlive = false; return; }

        _actionAccumulator += dt;
        if (_actionAccumulator < Behavior.ActionInterval) return;
        _actionAccumulator -= Behavior.ActionInterval;

        Behavior.PerformAction?.Invoke(this, terrain);
    }

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        if (!IsAlive) return;
        int half = BoundingBox.HalfWidth;
        uint color = State == MachineState.Template
            ? Tweaks.Colors.MachineTemplate.ToArgb()
            : Behavior.ActiveColor.ToArgb();

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
