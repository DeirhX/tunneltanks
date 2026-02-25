namespace Tunnerer.Core.Collision;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;

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

    /// <summary>
    /// Calls <paramref name="visitor"/> with each entity index in the 3x3 neighborhood
    /// around <paramref name="pos"/>. Returns true early if any visitor call returns true.
    /// </summary>
    public bool ForEachNearbyEntity(Position pos, Func<int, bool> visitor)
    {
        int sx = Math.Clamp(pos.X / Tweaks.Perf.SectorSize, 0, _sectorsX - 1);
        int sy = Math.Clamp(pos.Y / Tweaks.Perf.SectorSize, 0, _sectorsY - 1);

        int minX = Math.Max(0, sx - 1), maxX = Math.Min(_sectorsX - 1, sx + 1);
        int minY = Math.Max(0, sy - 1), maxY = Math.Min(_sectorsY - 1, sy + 1);

        for (int ny = minY; ny <= maxY; ny++)
            for (int nx = minX; nx <= maxX; nx++)
            {
                var list = _sectors[nx + ny * _sectorsX];
                for (int i = 0; i < list.Count; i++)
                    if (visitor(list[i])) return true;
            }
        return false;
    }

    public void Clear()
    {
        foreach (var list in _sectors)
            list.Clear();
    }
}
