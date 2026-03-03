using Tunnerer.Core.Collision;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Entities.Machines;
using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

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
    [Fact]
    public void TestPoint_TerrainOnly_HitsRock()
    {
        var t = new TerrainGrid(new Size(100, 100));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        t.SetPixelRaw(new Position(50, 50), TerrainPixel.Rock);
        var solver = new CollisionSolver(t);

        bool hit = solver.TestPoint(new Position(50, 50),
            onTerrain: pix => Pixel.IsBlockingCollision(pix));
        Assert.True(hit);
    }

    [Fact]
    public void TestPoint_TerrainOnly_MissesBlank()
    {
        var t = new TerrainGrid(new Size(100, 100));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        bool hit = solver.TestPoint(new Position(50, 50),
            onTerrain: pix => Pixel.IsBlockingCollision(pix));
        Assert.False(hit);
    }

    [Fact]
    public void TestPoint_TankHit_UsesWorldSectors()
    {
        var t = new TerrainGrid(new Size(200, 200));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(100, 100), 0);
        tankBases.AddBase(new Position(150, 100), 1);
        var tankList = new TankList();
        tankList.AddTank(0, tankBases.GetSpawn(0)!);
        tankList.AddTank(1, tankBases.GetSpawn(1)!);
        var machines = new MachineList();

        solver.Update(tankList, machines);

        Tank? hitTank = null;
        bool hit = solver.TestPoint(new Position(100, 100),
            onTank: tank => { hitTank = tank; return true; });

        Assert.True(hit);
        Assert.NotNull(hitTank);
        Assert.Equal(0, hitTank.Color);
    }

    [Fact]
    public void TestPoint_TankMiss_WhenFarAway()
    {
        var t = new TerrainGrid(new Size(200, 200));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(100, 100), 0);
        var tankList = new TankList();
        tankList.AddTank(0, tankBases.GetSpawn(0)!);
        var machines = new MachineList();

        solver.Update(tankList, machines);

        bool hit = solver.TestPoint(new Position(50, 50),
            onTank: _ => true);

        Assert.False(hit);
    }

    [Fact]
    public void TestPoint_TankTransparentSpritePixel_DoesNotHit()
    {
        var t = new TerrainGrid(new Size(200, 200));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(100, 100), 0);
        var tankList = new TankList();
        var tank = tankList.AddTank(0, tankBases.GetSpawn(0)!);
        tank.Direction = 1; // Direction 1 has transparent corners in the 7x7 sprite.
        var machines = new MachineList();

        solver.Update(tankList, machines);

        bool hit = solver.TestPoint(new Position(97, 97),
            onTank: _ => true);

        Assert.False(hit);
    }

    [Fact]
    public void TestPoint_MachineHit_WhenPlanted()
    {
        var t = new TerrainGrid(new Size(100, 100));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankList = new TankList();
        var machines = new MachineList();
        var machine = new Machine(new Position(50, 50), MachineType.Harvester, 0);
        machine.State = MachineState.Planted;
        machines.Add(machine);

        solver.Update(tankList, machines);

        Machine? hitMachine = null;
        bool hit = solver.TestPoint(new Position(50, 50),
            onMachine: m => { hitMachine = m; return true; });

        Assert.True(hit);
        Assert.NotNull(hitMachine);
    }

    [Fact]
    public void TestPoint_VisitorOrder_TankBeforeTerrain()
    {
        var t = new TerrainGrid(new Size(200, 200));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        t.SetPixelRaw(new Position(100, 100), TerrainPixel.Rock);
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(100, 100), 0);
        var tankList = new TankList();
        tankList.AddTank(0, tankBases.GetSpawn(0)!);
        var machines = new MachineList();
        solver.Update(tankList, machines);

        string hitType = "";
        solver.TestPoint(new Position(100, 100),
            onTank: _ => { hitType = "tank"; return true; },
            onTerrain: _ => { hitType = "terrain"; return true; });

        Assert.Equal("tank", hitType);
    }

    [Fact]
    public void TestPoint_NullCallbacks_SkipsCategories()
    {
        var t = new TerrainGrid(new Size(100, 100));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        t.SetPixelRaw(new Position(50, 50), TerrainPixel.Rock);
        var solver = new CollisionSolver(t);

        bool hit = solver.TestPoint(new Position(50, 50));
        Assert.False(hit);
    }

    [Fact]
    public void TestPoint_DeadTank_IsSkipped()
    {
        var t = new TerrainGrid(new Size(200, 200));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(100, 100), 0);
        var tankList = new TankList();
        var tank = tankList.AddTank(0, tankBases.GetSpawn(0)!);
        tank.Die();
        var machines = new MachineList();
        solver.Update(tankList, machines);

        bool hit = solver.TestPoint(new Position(100, 100),
            onTank: _ => true);
        Assert.False(hit);
    }

    [Fact]
    public void TestPoint_MachineNotPlanted_IsSkipped()
    {
        var t = new TerrainGrid(new Size(100, 100));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankList = new TankList();
        var machines = new MachineList();
        var machine = new Machine(new Position(50, 50), MachineType.Harvester, 0);
        machine.State = MachineState.Template;
        machines.Add(machine);
        solver.Update(tankList, machines);

        bool hit = solver.TestPoint(new Position(50, 50),
            onMachine: _ => true);
        Assert.False(hit);
    }

    [Fact]
    public void TestPoint_MachineTransporting_IsSkipped()
    {
        var t = new TerrainGrid(new Size(100, 100));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankList = new TankList();
        var machines = new MachineList();
        var machine = new Machine(new Position(50, 50), MachineType.Charger, 0);
        machine.State = MachineState.Transporting;
        machines.Add(machine);
        solver.Update(tankList, machines);

        bool hit = solver.TestPoint(new Position(50, 50),
            onMachine: _ => true);
        Assert.False(hit);
    }

    [Fact]
    public void TestPoint_MachineMiss_WhenOutsideBoundingBox()
    {
        var t = new TerrainGrid(new Size(100, 100));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankList = new TankList();
        var machines = new MachineList();
        var machine = new Machine(new Position(50, 50), MachineType.Harvester, 0);
        machine.State = MachineState.Planted;
        machines.Add(machine);
        solver.Update(tankList, machines);

        bool hit = solver.TestPoint(new Position(60, 60),
            onMachine: _ => true);
        Assert.False(hit);
    }

    [Fact]
    public void TestPoint_TankCallbackReturnsFalse_FallsThrough()
    {
        var t = new TerrainGrid(new Size(200, 200));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        t.SetPixelRaw(new Position(100, 100), TerrainPixel.DirtHigh);
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(100, 100), 0);
        var tankList = new TankList();
        tankList.AddTank(0, tankBases.GetSpawn(0)!);
        var machines = new MachineList();
        solver.Update(tankList, machines);

        string hitCategory = "none";
        solver.TestPoint(new Position(100, 100),
            onTank: _ => { hitCategory = "tank-rejected"; return false; },
            onTerrain: _ => { hitCategory = "terrain"; return true; });

        Assert.Equal("terrain", hitCategory);
    }

    [Fact]
    public void TestPoint_TankMissThenTerrainHit_ReturnsTrueFromTerrain()
    {
        var t = new TerrainGrid(new Size(200, 200));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        t.SetPixelRaw(new Position(50, 50), TerrainPixel.Rock);
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(100, 100), 0);
        var tankList = new TankList();
        tankList.AddTank(0, tankBases.GetSpawn(0)!);
        var machines = new MachineList();
        solver.Update(tankList, machines);

        bool hit = solver.TestPoint(new Position(50, 50),
            onTank: _ => true,
            onTerrain: pix => Pixel.IsAnyCollision(pix));

        Assert.True(hit);
    }

    [Fact]
    public void TestPoint_OutsideTerrainBounds_TerrainCallbackNotCalled()
    {
        var t = new TerrainGrid(new Size(100, 100));
        var solver = new CollisionSolver(t);

        bool terrainCalled = false;
        bool hit = solver.TestPoint(new Position(-5, -5),
            onTerrain: _ => { terrainCalled = true; return true; });

        Assert.False(hit);
        Assert.False(terrainCalled);
    }

    [Fact]
    public void TestPoint_WithoutUpdate_TankCallbackNotCalled()
    {
        var t = new TerrainGrid(new Size(100, 100));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        bool tankCalled = false;
        solver.TestPoint(new Position(50, 50),
            onTank: _ => { tankCalled = true; return true; });

        Assert.False(tankCalled);
    }

    [Fact]
    public void TestPoint_MultipleTanks_FindsCorrectOne()
    {
        var t = new TerrainGrid(new Size(400, 400));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(100, 100), 0);
        tankBases.AddBase(new Position(300, 300), 1);
        var tankList = new TankList();
        tankList.AddTank(0, tankBases.GetSpawn(0)!);
        tankList.AddTank(1, tankBases.GetSpawn(1)!);
        var machines = new MachineList();
        solver.Update(tankList, machines);

        Tank? hitTank = null;
        solver.TestPoint(new Position(300, 300),
            onTank: tank => { hitTank = tank; return true; });

        Assert.NotNull(hitTank);
        Assert.Equal(1, hitTank.Color);
    }

    [Fact]
    public void Update_RebuildsSectors_TankPositionChangeDetected()
    {
        var t = new TerrainGrid(new Size(400, 400));
        for (int i = 0; i < t.Size.Area; i++) t[i] = TerrainPixel.Blank;
        var solver = new CollisionSolver(t);

        var tankBases = new TankBases();
        tankBases.AddBase(new Position(50, 50), 0);
        var tankList = new TankList();
        var tank = tankList.AddTank(0, tankBases.GetSpawn(0)!);
        var machines = new MachineList();

        solver.Update(tankList, machines);
        bool hitAtOld = solver.TestPoint(new Position(50, 50), onTank: _ => true);
        Assert.True(hitAtOld);

        tank.Position = new Position(350, 350);
        solver.Update(tankList, machines);

        bool hitAtOldAfterMove = solver.TestPoint(new Position(50, 50), onTank: _ => true);
        bool hitAtNew = solver.TestPoint(new Position(350, 350), onTank: _ => true);
        Assert.False(hitAtOldAfterMove);
        Assert.True(hitAtNew);
    }
}

public class TerrainBehaviorTests
{
    [Theory]
    [InlineData(TerrainPixel.Rock, true, false)]
    [InlineData(TerrainPixel.ConcreteHigh, true, false)]
    [InlineData(TerrainPixel.ConcreteLow, true, false)]
    [InlineData(TerrainPixel.DirtHigh, false, true)]
    [InlineData(TerrainPixel.DirtLow, false, true)]
    [InlineData(TerrainPixel.Blank, false, false)]
    [InlineData(TerrainPixel.DecalHigh, false, false)]
    public void BehaviorTable_MatchesLegacyMethods(TerrainPixel pix, bool blocksMovement, bool softCollision)
    {
        var b = Pixel.GetBehavior(pix);
        Assert.Equal(blocksMovement, b.BlocksMovement);
        Assert.Equal(softCollision, b.SoftCollision);
        Assert.Equal(blocksMovement || softCollision, b.IsAnyCollision);
    }

    [Fact]
    public void BaseRange_AllBlockMovement()
    {
        for (byte b = (byte)TerrainPixel.BaseMin; b <= (byte)TerrainPixel.BaseMax; b++)
        {
            var pix = (TerrainPixel)b;
            Assert.True(Pixel.IsBlockingCollision(pix), $"Base pixel {pix} should block movement");
            Assert.True(Pixel.IsBase(pix), $"Base pixel {pix} should be recognized as base");
        }
    }

    [Fact]
    public void BaseBarrier_DoesNotBlockMovement()
    {
        Assert.False(Pixel.IsBlockingCollision(TerrainPixel.BaseBarrier));
        Assert.False(Pixel.IsBase(TerrainPixel.BaseBarrier));
    }

    [Theory]
    [InlineData(TerrainPixel.DirtHigh)]
    [InlineData(TerrainPixel.DirtLow)]
    [InlineData(TerrainPixel.DirtGrow)]
    public void Diggable_IncludesAllDirtVariants(TerrainPixel pix)
    {
        Assert.True(Pixel.IsDiggable(pix));
    }

    [Fact]
    public void Torchable_IncludesDiggableAndMineral()
    {
        Assert.True(Pixel.IsTorchable(TerrainPixel.DirtHigh));
        Assert.True(Pixel.IsTorchable(TerrainPixel.Rock));
        Assert.True(Pixel.IsTorchable(TerrainPixel.ConcreteHigh));
        Assert.False(Pixel.IsTorchable(TerrainPixel.Blank));
    }

    [Fact]
    public void GetColor_AllDefinedPixels_NotMagenta()
    {
        var magenta = new Color(0xff, 0x00, 0xff);
        TerrainPixel[] defined = [
            TerrainPixel.Blank, TerrainPixel.DirtHigh, TerrainPixel.DirtLow,
            TerrainPixel.DirtGrow, TerrainPixel.Rock, TerrainPixel.DecalHigh,
            TerrainPixel.DecalLow, TerrainPixel.ConcreteLow, TerrainPixel.ConcreteHigh,
            TerrainPixel.EnergyLow, TerrainPixel.EnergyMedium, TerrainPixel.EnergyHigh,
            TerrainPixel.BaseBarrier, TerrainPixel.BaseMin, TerrainPixel.BaseMax,
        ];

        foreach (var pix in defined)
            Assert.NotEqual(magenta, Pixel.GetColor(pix));
    }

    [Fact]
    public void GetColor_UndefinedPixel_ReturnsMagenta()
    {
        var magenta = new Color(0xff, 0x00, 0xff);
        var color = Pixel.GetColor((TerrainPixel)255);
        Assert.Equal(magenta, color);
    }

    [Theory]
    [InlineData(TerrainPixel.EnergyLow)]
    [InlineData(TerrainPixel.EnergyMedium)]
    [InlineData(TerrainPixel.EnergyHigh)]
    public void Energy_IsCollectible(TerrainPixel pix)
    {
        Assert.True(Pixel.IsEnergy(pix));
        Assert.False(Pixel.IsBlockingCollision(pix));
        Assert.False(Pixel.IsSoftCollision(pix));
    }

    [Fact]
    public void Rock_IsMineral_NotConcrete()
    {
        Assert.True(Pixel.IsMineral(TerrainPixel.Rock));
        Assert.True(Pixel.IsRock(TerrainPixel.Rock));
        Assert.False(Pixel.IsConcrete(TerrainPixel.Rock));
    }

    [Theory]
    [InlineData(TerrainPixel.ConcreteHigh)]
    [InlineData(TerrainPixel.ConcreteLow)]
    public void Concrete_IsMineral_AndConcrete(TerrainPixel pix)
    {
        Assert.True(Pixel.IsMineral(pix));
        Assert.True(Pixel.IsConcrete(pix));
        Assert.False(Pixel.IsRock(pix));
    }

    [Theory]
    [InlineData(TerrainPixel.DecalHigh)]
    [InlineData(TerrainPixel.DecalLow)]
    public void Scorched_IsRecognized(TerrainPixel pix)
    {
        Assert.True(Pixel.IsScorched(pix));
        Assert.False(Pixel.IsBlockingCollision(pix));
        Assert.False(Pixel.IsDiggable(pix));
    }

    [Fact]
    public void DirtGrow_IsDiggable_ButNotSoftCollision()
    {
        Assert.True(Pixel.IsDiggable(TerrainPixel.DirtGrow));
        Assert.False(Pixel.IsSoftCollision(TerrainPixel.DirtGrow));
        Assert.False(Pixel.IsDirt(TerrainPixel.DirtGrow));
    }

    [Fact]
    public void GetBehavior_ReturnsRefToSameInstance()
    {
        ref readonly var b1 = ref Pixel.GetBehavior(TerrainPixel.Rock);
        ref readonly var b2 = ref Pixel.GetBehavior(TerrainPixel.Rock);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void Blank_IsEmpty()
    {
        Assert.True(Pixel.IsEmpty(TerrainPixel.Blank));
        Assert.False(Pixel.IsEmpty(TerrainPixel.Rock));
        Assert.False(Pixel.IsEmpty(TerrainPixel.DirtHigh));
    }

    [Fact]
    public void Blank_HasBlackColor()
    {
        var black = new Color(0, 0, 0);
        Assert.Equal(black, Pixel.GetColor(TerrainPixel.Blank));
    }

    [Theory]
    [InlineData(TerrainPixel.Rock)]
    [InlineData(TerrainPixel.ConcreteHigh)]
    [InlineData(TerrainPixel.ConcreteLow)]
    [InlineData(TerrainPixel.DirtHigh)]
    [InlineData(TerrainPixel.DirtLow)]
    [InlineData(TerrainPixel.DirtGrow)]
    public void Torchable_MatchesDerivedProperty(TerrainPixel pix)
    {
        var b = Pixel.GetBehavior(pix);
        Assert.Equal(b.Diggable || b.Mineral, b.Torchable);
        Assert.True(Pixel.IsTorchable(pix));
    }

    [Theory]
    [InlineData(TerrainPixel.Blank)]
    [InlineData(TerrainPixel.DecalHigh)]
    [InlineData(TerrainPixel.EnergyLow)]
    [InlineData(TerrainPixel.BaseBarrier)]
    public void NotTorchable_PixelTypes(TerrainPixel pix)
    {
        Assert.False(Pixel.IsTorchable(pix));
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
