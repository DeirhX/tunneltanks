namespace TunnelTanks.Core.Collision;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;

public class WorldSectors
{
    private readonly List<int>[] _sectors;
    private readonly int _sectorsX;
    private readonly int _sectorsY;

    public WorldSectors(Size worldSize)
    {
        _sectorsX = (worldSize.X + Tweaks.Perf.SectorSize - 1) / Tweaks.Perf.SectorSize;
        _sectorsY = (worldSize.Y + Tweaks.Perf.SectorSize - 1) / Tweaks.Perf.SectorSize;
        _sectors = new List<int>[_sectorsX * _sectorsY];
        for (int i = 0; i < _sectors.Length; i++)
            _sectors[i] = new List<int>();
    }

    public int SectorIdForPosition(Position pos)
    {
        int sx = Math.Clamp(pos.X / Tweaks.Perf.SectorSize, 0, _sectorsX - 1);
        int sy = Math.Clamp(pos.Y / Tweaks.Perf.SectorSize, 0, _sectorsY - 1);
        return sx + sy * _sectorsX;
    }

    public IReadOnlyList<int> GetEntitiesInSector(int sectorId) => _sectors[sectorId];

    public void AddEntity(int sectorId, int entityId) => _sectors[sectorId].Add(entityId);

    public void RemoveEntity(int sectorId, int entityId) => _sectors[sectorId].Remove(entityId);

    public void Clear()
    {
        foreach (var list in _sectors)
            list.Clear();
    }
}
