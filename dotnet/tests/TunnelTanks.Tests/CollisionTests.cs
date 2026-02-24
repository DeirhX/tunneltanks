using TunnelTanks.Core.Collision;
using TunnelTanks.Core.Terrain;
using TunnelTanks.Core.Types;

namespace TunnelTanks.Tests;

public class CollisionTests
{
    [Fact]
    public void TestPixel_Rock_IsBlocked()
    {
        var t = new TerrainGrid(new Size(10, 10));
        t.SetPixelRaw(new Position(5, 5), TerrainPixel.Rock);
        var solver = new CollisionSolver(t);
        Assert.Equal(CollisionType.Blocked, solver.TestPixel(new Position(5, 5)));
    }

    [Fact]
    public void TestPixel_Dirt_IsSoftCollision()
    {
        var t = new TerrainGrid(new Size(10, 10));
        t.SetPixelRaw(new Position(5, 5), TerrainPixel.DirtHigh);
        var solver = new CollisionSolver(t);
        Assert.Equal(CollisionType.Dirt, solver.TestPixel(new Position(5, 5)));
    }

    [Fact]
    public void TestPixel_Blank_IsNone()
    {
        var t = new TerrainGrid(new Size(10, 10));
        t.SetPixelRaw(new Position(5, 5), TerrainPixel.Blank);
        var solver = new CollisionSolver(t);
        Assert.Equal(CollisionType.None, solver.TestPixel(new Position(5, 5)));
    }

    [Fact]
    public void TestPixel_OutOfBounds_IsBlocked()
    {
        var t = new TerrainGrid(new Size(10, 10));
        var solver = new CollisionSolver(t);
        Assert.Equal(CollisionType.Blocked, solver.TestPixel(new Position(-1, 5)));
    }

    [Fact]
    public void TestShape_NoCollisionInEmptyArea()
    {
        var t = new TerrainGrid(new Size(20, 20));
        for (int i = 0; i < 400; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        byte[] shape = [1, 1, 1, 1, 1, 1, 1, 1, 1]; // 3x3 filled
        Assert.Equal(CollisionType.None, solver.TestShape(new Position(10, 10), shape, 3, 3));
    }

    [Fact]
    public void TestShape_DetectsRockCollision()
    {
        var t = new TerrainGrid(new Size(20, 20));
        for (int i = 0; i < 400; i++) t[i] = TerrainPixel.Blank;
        t.SetPixelRaw(new Position(11, 10), TerrainPixel.Rock);
        var solver = new CollisionSolver(t);

        byte[] shape = [1, 1, 1, 1, 1, 1, 1, 1, 1]; // 3x3 filled
        Assert.Equal(CollisionType.Blocked, solver.TestShape(new Position(10, 10), shape, 3, 3));
    }

    [Fact]
    public void TestShape_DetectsDirtCollision()
    {
        var t = new TerrainGrid(new Size(20, 20));
        for (int i = 0; i < 400; i++) t[i] = TerrainPixel.Blank;
        t.SetPixelRaw(new Position(11, 10), TerrainPixel.DirtHigh);
        var solver = new CollisionSolver(t);

        byte[] shape = [1, 1, 1, 1, 1, 1, 1, 1, 1];
        Assert.Equal(CollisionType.Dirt, solver.TestShape(new Position(10, 10), shape, 3, 3));
    }

    [Fact]
    public void TestShape_SkipsZeroPixels()
    {
        var t = new TerrainGrid(new Size(20, 20));
        for (int i = 0; i < 400; i++) t[i] = TerrainPixel.Blank;
        t.SetPixelRaw(new Position(9, 9), TerrainPixel.Rock);
        var solver = new CollisionSolver(t);

        byte[] shape = [0, 1, 1, 1, 1, 1, 1, 1, 1]; // top-left pixel masked out
        Assert.Equal(CollisionType.None, solver.TestShape(new Position(10, 10), shape, 3, 3));
    }
}

public class RaycasterTests
{
    [Fact]
    public void BresenhamLine_VisitsEndpoints()
    {
        var visited = new List<Position>();
        Raycaster.BresenhamLine(new Position(0, 0), new Position(5, 3), p => visited.Add(p));

        Assert.Equal(new Position(0, 0), visited[0]);
        Assert.Equal(new Position(5, 3), visited[^1]);
    }

    [Fact]
    public void BresenhamLine_HorizontalLine()
    {
        var visited = new List<Position>();
        Raycaster.BresenhamLine(new Position(0, 5), new Position(10, 5), p => visited.Add(p));

        Assert.Equal(11, visited.Count);
        Assert.All(visited, p => Assert.Equal(5, p.Y));
    }

    [Fact]
    public void BresenhamLine_VerticalLine()
    {
        var visited = new List<Position>();
        Raycaster.BresenhamLine(new Position(3, 0), new Position(3, 7), p => visited.Add(p));

        Assert.Equal(8, visited.Count);
        Assert.All(visited, p => Assert.Equal(3, p.X));
    }

    [Fact]
    public void BresenhamLine_SinglePoint()
    {
        var visited = new List<Position>();
        Raycaster.BresenhamLine(new Position(5, 5), new Position(5, 5), p => visited.Add(p));

        Assert.Single(visited);
        Assert.Equal(new Position(5, 5), visited[0]);
    }

    [Fact]
    public void BresenhamLine_AllPixelsAreConnected()
    {
        var visited = new List<Position>();
        Raycaster.BresenhamLine(new Position(0, 0), new Position(20, 13), p => visited.Add(p));

        for (int i = 1; i < visited.Count; i++)
        {
            int dx = Math.Abs(visited[i].X - visited[i - 1].X);
            int dy = Math.Abs(visited[i].Y - visited[i - 1].Y);
            Assert.True(dx <= 1 && dy <= 1, $"Gap between {visited[i - 1]} and {visited[i]}");
        }
    }

    [Fact]
    public void CastRay_ProducesCorrectStepCount()
    {
        var points = Raycaster.CastRay(
            new PositionF(5f, 5f), new VectorF(1f, 0f), 10).ToList();
        Assert.Equal(10, points.Count);
    }

    [Fact]
    public void CastRay_MovesInCorrectDirection()
    {
        var points = Raycaster.CastRay(
            new PositionF(0f, 0f), new VectorF(1f, 0f), 5).ToList();

        for (int i = 0; i < points.Count; i++)
            Assert.Equal(i + 1, points[i].X);
    }
}
