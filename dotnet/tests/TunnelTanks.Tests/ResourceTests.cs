using TunnelTanks.Core.Resources;

namespace TunnelTanks.Tests;

public class ResourceTests
{
    [Fact]
    public void Reactor_Pay_DeductsWhenAffordable()
    {
        var r = new Reactor(100, 50, 200, 100);
        var cost = new ReactorState(30, 10);
        Assert.True(r.Pay(cost));
        Assert.Equal(70, r.Energy);
        Assert.Equal(40, r.Health);
    }

    [Fact]
    public void Reactor_Pay_RefusesWhenUnaffordable()
    {
        var r = new Reactor(10, 50, 200, 100);
        var cost = new ReactorState(30, 10);
        Assert.False(r.Pay(cost));
        Assert.Equal(10, r.Energy); // unchanged
    }

    [Fact]
    public void Reactor_Add_ClampsToCapacity()
    {
        var r = new Reactor(180, 90, 200, 100);
        r.Add(new ReactorState(50, 30));
        Assert.Equal(200, r.Energy);
        Assert.Equal(100, r.Health);
    }

    [Fact]
    public void Reactor_Exhaust_ClampsToZero()
    {
        var r = new Reactor(10, 5, 200, 100);
        bool survived = r.Exhaust(new ReactorState(20, 10));
        Assert.False(survived);
        Assert.Equal(0, r.Energy);
        Assert.Equal(0, r.Health);
    }

    [Fact]
    public void Reactor_Exhaust_ReturnsTrueIfNotNegative()
    {
        var r = new Reactor(30, 20, 200, 100);
        bool survived = r.Exhaust(new ReactorState(10, 5));
        Assert.True(survived);
        Assert.Equal(20, r.Energy);
        Assert.Equal(15, r.Health);
    }

    [Fact]
    public void Reactor_Absorb_TransfersAndClamps()
    {
        var a = new Reactor(150, 80, 200, 100);
        var b = new Reactor(100, 40, 200, 100);
        a.Absorb(b);

        Assert.Equal(200, a.Energy);
        Assert.Equal(100, a.Health);
        // Overflow goes back to source
        Assert.Equal(50, b.Energy);
        Assert.Equal(20, b.Health);
    }

    [Fact]
    public void MaterialContainer_Pay_Works()
    {
        var mc = new MaterialContainer(50, 30, 100, 100);
        Assert.True(mc.Pay(new MaterialAmount(20, 10)));
        Assert.Equal(30, mc.Dirt);
        Assert.Equal(20, mc.Minerals);
    }

    [Fact]
    public void MaterialContainer_Pay_RefusesInsufficient()
    {
        var mc = new MaterialContainer(10, 30, 100, 100);
        Assert.False(mc.Pay(new MaterialAmount(20, 10)));
        Assert.Equal(10, mc.Dirt); // unchanged
    }

    [Fact]
    public void MaterialContainer_Add_ClampsToCapacity()
    {
        var mc = new MaterialContainer(90, 80, 100, 100);
        mc.Add(new MaterialAmount(30, 40));
        Assert.Equal(100, mc.Dirt);
        Assert.Equal(100, mc.Minerals);
    }

    [Fact]
    public void MaterialContainer_Absorb_TransfersAndClamps()
    {
        var a = new MaterialContainer(80, 70, 100, 100);
        var b = new MaterialContainer(50, 60, 100, 100);
        a.Absorb(b);

        Assert.Equal(100, a.Dirt);
        Assert.Equal(100, a.Minerals);
        Assert.Equal(30, b.Dirt);
        Assert.Equal(30, b.Minerals);
    }

    [Fact]
    public void ReactorState_Arithmetic()
    {
        var a = new ReactorState(10, 20);
        var b = new ReactorState(3, 7);
        var sum = a + b;
        var diff = a - b;

        Assert.Equal(13, sum.Energy);
        Assert.Equal(27, sum.Health);
        Assert.Equal(7, diff.Energy);
        Assert.Equal(13, diff.Health);
    }

    [Fact]
    public void ReactorState_IsNegative()
    {
        Assert.False(new ReactorState(1, 1).IsNegative);
        Assert.True(new ReactorState(-1, 1).IsNegative);
        Assert.True(new ReactorState(1, -1).IsNegative);
    }

    [Fact]
    public void ReactorState_TrimNegative()
    {
        var s = new ReactorState(-5, -10);
        s.TrimNegative();
        Assert.Equal(0, s.Energy);
        Assert.Equal(0, s.Health);
    }

    [Fact]
    public void MaterialAmount_Arithmetic()
    {
        var a = new MaterialAmount(10, 20);
        var b = new MaterialAmount(3, 7);
        Assert.Equal(13, (a + b).Dirt);
        Assert.Equal(27, (a + b).Minerals);
        Assert.Equal(7, (a - b).Dirt);
        Assert.Equal(13, (a - b).Minerals);
    }
}
