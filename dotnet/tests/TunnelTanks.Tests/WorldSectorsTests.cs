using TunnelTanks.Core.Collision;
using TunnelTanks.Core.Config;
using TunnelTanks.Core.Types;

namespace TunnelTanks.Tests;

public class WorldSectorsTests
{
    [Fact]
    public void SectorIdForPosition_OriginIsZero()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        Assert.Equal(0, sectors.SectorIdForPosition(new Position(0, 0)));
    }

    [Fact]
    public void SectorIdForPosition_PositionsInSameSector_ReturnSameId()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int ss = Tweaks.Perf.SectorSize;
        int id1 = sectors.SectorIdForPosition(new Position(0, 0));
        int id2 = sectors.SectorIdForPosition(new Position(ss - 1, ss - 1));
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void SectorIdForPosition_AdjacentSectors_DifferentIds()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int ss = Tweaks.Perf.SectorSize;
        int id1 = sectors.SectorIdForPosition(new Position(ss - 1, 0));
        int id2 = sectors.SectorIdForPosition(new Position(ss, 0));
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SectorIdForPosition_NegativePosition_ClampsToZero()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int id = sectors.SectorIdForPosition(new Position(-10, -10));
        Assert.Equal(0, id);
    }

    [Fact]
    public void SectorIdForPosition_BeyondBounds_ClampsToMax()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int idMax = sectors.SectorIdForPosition(new Position(999, 999));
        int idCorner = sectors.SectorIdForPosition(new Position(255, 255));
        Assert.Equal(idCorner, idMax);
    }

    [Fact]
    public void AddEntity_And_GetEntities_RoundTrips()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int sid = sectors.SectorIdForPosition(new Position(100, 100));
        sectors.AddEntity(sid, 42);

        var entities = sectors.GetEntitiesInSector(sid);
        Assert.Contains(42, entities);
    }

    [Fact]
    public void RemoveEntity_RemovesFromSector()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int sid = sectors.SectorIdForPosition(new Position(100, 100));
        sectors.AddEntity(sid, 7);
        sectors.RemoveEntity(sid, 7);

        var entities = sectors.GetEntitiesInSector(sid);
        Assert.DoesNotContain(7, entities);
    }

    [Fact]
    public void Clear_RemovesAllEntities()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int sid1 = sectors.SectorIdForPosition(new Position(10, 10));
        int sid2 = sectors.SectorIdForPosition(new Position(200, 200));
        sectors.AddEntity(sid1, 1);
        sectors.AddEntity(sid2, 2);

        sectors.Clear();

        Assert.Empty(sectors.GetEntitiesInSector(sid1));
        Assert.Empty(sectors.GetEntitiesInSector(sid2));
    }

    [Fact]
    public void ForEachNearbyEntity_FindsEntityInSameSector()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        var pos = new Position(100, 100);
        int sid = sectors.SectorIdForPosition(pos);
        sectors.AddEntity(sid, 5);

        var found = new List<int>();
        sectors.ForEachNearbyEntity(pos, id => { found.Add(id); return false; });

        Assert.Contains(5, found);
    }

    [Fact]
    public void ForEachNearbyEntity_FindsEntityInAdjacentSector()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int ss = Tweaks.Perf.SectorSize;

        var queryPos = new Position(ss + 1, ss + 1);
        int adjacentSid = sectors.SectorIdForPosition(new Position(0, 0));
        sectors.AddEntity(adjacentSid, 99);

        var found = new List<int>();
        sectors.ForEachNearbyEntity(queryPos, id => { found.Add(id); return false; });

        Assert.Contains(99, found);
    }

    [Fact]
    public void ForEachNearbyEntity_DoesNotFindEntityInDistantSector()
    {
        var sectors = new WorldSectors(new Size(512, 512));
        int ss = Tweaks.Perf.SectorSize;

        int distantSid = sectors.SectorIdForPosition(new Position(ss * 4, ss * 4));
        sectors.AddEntity(distantSid, 77);

        var found = new List<int>();
        sectors.ForEachNearbyEntity(new Position(0, 0), id => { found.Add(id); return false; });

        Assert.DoesNotContain(77, found);
    }

    [Fact]
    public void ForEachNearbyEntity_EarlyExit_StopsOnTrue()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int sid = sectors.SectorIdForPosition(new Position(50, 50));
        sectors.AddEntity(sid, 1);
        sectors.AddEntity(sid, 2);
        sectors.AddEntity(sid, 3);

        int visitCount = 0;
        bool result = sectors.ForEachNearbyEntity(new Position(50, 50), id =>
        {
            visitCount++;
            return id == 2;
        });

        Assert.True(result);
        Assert.Equal(2, visitCount);
    }

    [Fact]
    public void ForEachNearbyEntity_ReturnsFalse_WhenNoEntityMatchesInNeighborhood()
    {
        var sectors = new WorldSectors(new Size(256, 256));

        bool result = sectors.ForEachNearbyEntity(new Position(128, 128), _ => false);
        Assert.False(result);
    }

    [Fact]
    public void ForEachNearbyEntity_CornerPosition_DoesNotThrow()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        int sid = sectors.SectorIdForPosition(new Position(0, 0));
        sectors.AddEntity(sid, 10);

        var found = new List<int>();
        sectors.ForEachNearbyEntity(new Position(0, 0), id => { found.Add(id); return false; });
        Assert.Contains(10, found);
    }

    [Fact]
    public void ForEachNearbyEntity_BottomRightCorner_DoesNotThrow()
    {
        var sectors = new WorldSectors(new Size(256, 256));
        var pos = new Position(255, 255);
        int sid = sectors.SectorIdForPosition(pos);
        sectors.AddEntity(sid, 20);

        var found = new List<int>();
        sectors.ForEachNearbyEntity(pos, id => { found.Add(id); return false; });
        Assert.Contains(20, found);
    }
}
