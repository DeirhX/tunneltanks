namespace TunnelTanks.Core.Entities.Machines;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Terrain;

public class MachineList
{
    private readonly List<Machine> _machines = new();

    public IReadOnlyList<Machine> Machines => _machines;

    public Machine Add(Machine machine)
    {
        _machines.Add(machine);
        return machine;
    }

    public void Advance(Terrain terrain, TimeSpan dt)
    {
        for (int i = _machines.Count - 1; i >= 0; i--)
        {
            if (!_machines[i].IsAlive) { _machines.RemoveAt(i); continue; }
            _machines[i].Advance(terrain, dt);
        }
    }

    public Machine? TestCollide(Position pos)
    {
        foreach (var m in _machines)
            if (m.IsAlive && m.TestCollide(pos))
                return m;
        return null;
    }

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        foreach (var m in _machines)
            m.Draw(surface, surfaceWidth, surfaceHeight);
    }

    public void RemoveAll() => _machines.Clear();
}
