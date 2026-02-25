using Tunnerer.Core.Config;
using Tunnerer.Core.Entities.Machines;
using Tunnerer.Core.Entities.Projectiles;
using Tunnerer.Core.Types;

namespace Tunnerer.Tests;

public class ProjectileBehaviorTests
{
    [Fact]
    public void AllProjectileTypes_HaveBehavior()
    {
        foreach (var type in Enum.GetValues<ProjectileType>())
        {
            int idx = (int)type;
            Assert.True(idx < ProjectileList.Behaviors.Length,
                $"Behavior array too small for {type} (index {idx})");
            Assert.NotNull(ProjectileList.Behaviors[idx]);
        }
    }

    [Fact]
    public void AllBehaviors_HaveAdvanceDelegate()
    {
        foreach (var type in Enum.GetValues<ProjectileType>())
        {
            var behavior = ProjectileList.Behaviors[(int)type];
            Assert.NotNull(behavior.Advance);
        }
    }

    [Fact]
    public void AllBehaviors_HaveNonDefaultDrawColor()
    {
        foreach (var type in Enum.GetValues<ProjectileType>())
        {
            var behavior = ProjectileList.Behaviors[(int)type];
            Assert.NotEqual(default, behavior.DrawColor);
        }
    }

    [Fact]
    public void Bullet_DrawColor_IsFireHot()
    {
        var behavior = ProjectileList.Behaviors[(int)ProjectileType.Bullet];
        Assert.Equal(Tweaks.Colors.FireHot, behavior.DrawColor);
    }

    [Fact]
    public void ConcreteFoam_DrawColor_IsConcrete()
    {
        var behavior = ProjectileList.Behaviors[(int)ProjectileType.ConcreteFoam];
        Assert.Equal(Tweaks.Colors.Concrete, behavior.DrawColor);
    }

    [Fact]
    public void DirtFoam_DrawColor_IsDirtProjectile()
    {
        var behavior = ProjectileList.Behaviors[(int)ProjectileType.DirtFoam];
        Assert.Equal(Tweaks.Colors.DirtProjectile, behavior.DrawColor);
    }

    [Fact]
    public void BehaviorArray_HasNoNullGaps()
    {
        for (int i = 0; i < ProjectileList.Behaviors.Length; i++)
            Assert.NotNull(ProjectileList.Behaviors[i]);
    }
}

public class MachineBehaviorTests
{
    [Fact]
    public void AllMachineTypes_HaveBehavior()
    {
        foreach (var type in Enum.GetValues<MachineType>())
        {
            var behavior = MachineBehaviors.Get(type);
            Assert.NotNull(behavior);
        }
    }

    [Fact]
    public void Get_InvalidType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MachineBehaviors.Get((MachineType)999));
    }

    [Fact]
    public void Harvester_HasPositiveActionInterval()
    {
        Assert.True(MachineBehaviors.Harvester.ActionInterval > TimeSpan.Zero);
    }

    [Fact]
    public void Harvester_HasPerformAction()
    {
        Assert.NotNull(MachineBehaviors.Harvester.PerformAction);
    }

    [Fact]
    public void Charger_HasNullPerformAction()
    {
        Assert.Null(MachineBehaviors.Charger.PerformAction);
    }

    [Fact]
    public void Harvester_BuildCost_MatchesTweaks()
    {
        Assert.Equal(Tweaks.Machine.HarvesterDirtCost, MachineBehaviors.Harvester.BuildCost.Dirt);
        Assert.Equal(0, MachineBehaviors.Harvester.BuildCost.Minerals);
    }

    [Fact]
    public void Charger_BuildCost_MatchesTweaks()
    {
        Assert.Equal(Tweaks.Machine.ChargerDirtCost, MachineBehaviors.Charger.BuildCost.Dirt);
        Assert.Equal(0, MachineBehaviors.Charger.BuildCost.Minerals);
    }

    [Fact]
    public void Harvester_ActiveColor_IsGreen()
    {
        Assert.Equal(Tweaks.Colors.Harvester, MachineBehaviors.Harvester.ActiveColor);
    }

    [Fact]
    public void Charger_ActiveColor_IsBlue()
    {
        Assert.Equal(Tweaks.Colors.Charger, MachineBehaviors.Charger.ActiveColor);
    }

    [Fact]
    public void Machine_StoresBehavior_FromType()
    {
        var machine = new Machine(new Position(50, 50), MachineType.Harvester, 0);
        Assert.Same(MachineBehaviors.Harvester, machine.Behavior);

        var charger = new Machine(new Position(50, 50), MachineType.Charger, 0);
        Assert.Same(MachineBehaviors.Charger, charger.Behavior);
    }
}
