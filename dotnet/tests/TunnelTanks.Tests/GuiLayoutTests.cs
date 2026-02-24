using TunnelTanks.Core.Config;
using TunnelTanks.Core.Entities;
using TunnelTanks.Core.Gui;
using TunnelTanks.Core.Types;

namespace TunnelTanks.Tests;

public class GuiLayoutTests
{
    private static readonly Size RenderSize = Tweaks.Screen.RenderSurfaceSize; // 320x200

    private Tank MakeTank(int color, Position pos)
    {
        var tb = new TankBase(pos, color);
        return new Tank(color, tb);
    }

    [Fact]
    public void TwoPlayerLayout_ViewRects_MatchCpp()
    {
        var screen = new Screen(RenderSize);
        var t1 = MakeTank(0, new Position(100, 100));
        var t2 = MakeTank(1, new Position(200, 100));
        screen.SetupTwoPlayers(t1, t2);

        // C++: player_view_one = {2, 2, 157, 183}
        Assert.Equal(2, screen.Views[0].ScreenRect.X);
        Assert.Equal(2, screen.Views[0].ScreenRect.Y);
        Assert.Equal(157, screen.Views[0].ScreenRect.Width);
        Assert.Equal(183, screen.Views[0].ScreenRect.Height);

        // C++: player_view_two = {162, 2, 157, 183}
        Assert.Equal(162, screen.Views[1].ScreenRect.X);
        Assert.Equal(2, screen.Views[1].ScreenRect.Y);
        Assert.Equal(157, screen.Views[1].ScreenRect.Width);
        Assert.Equal(183, screen.Views[1].ScreenRect.Height);
    }

    [Fact]
    public void TwoPlayerLayout_StatusBarRects_MatchCpp()
    {
        var screen = new Screen(RenderSize);
        var t1 = MakeTank(0, new Position(100, 100));
        var t2 = MakeTank(1, new Position(200, 100));
        screen.SetupTwoPlayers(t1, t2);

        // C++: health_energy_one = {2, 187, 150, 11}
        Assert.Equal(2, screen.StatusBars[0].ScreenRect.X);
        Assert.Equal(187, screen.StatusBars[0].ScreenRect.Y);
        Assert.Equal(150, screen.StatusBars[0].ScreenRect.Width);
        Assert.Equal(11, screen.StatusBars[0].ScreenRect.Height);

        // C++: health_energy_two = {169, 187, 150, 11}
        Assert.Equal(169, screen.StatusBars[1].ScreenRect.X);
        Assert.Equal(187, screen.StatusBars[1].ScreenRect.Y);
        Assert.Equal(150, screen.StatusBars[1].ScreenRect.Width);
        Assert.Equal(11, screen.StatusBars[1].ScreenRect.Height);
    }

    [Fact]
    public void SinglePlayerLayout_ViewRect_MatchesCpp()
    {
        var screen = new Screen(RenderSize);
        var t1 = MakeTank(0, new Position(100, 100));
        screen.SetupSinglePlayer(t1);

        // C++: player_view_rect = {2, 2, 316, 183}
        Assert.Equal(2, screen.Views[0].ScreenRect.X);
        Assert.Equal(2, screen.Views[0].ScreenRect.Y);
        Assert.Equal(316, screen.Views[0].ScreenRect.Width);
        Assert.Equal(183, screen.Views[0].ScreenRect.Height);
    }

    [Fact]
    public void SinglePlayerLayout_StatusBarRect_MatchesCpp()
    {
        var screen = new Screen(RenderSize);
        var t1 = MakeTank(0, new Position(100, 100));
        screen.SetupSinglePlayer(t1);

        // C++: tank_health_bars_rect = {9, 187, 304, 11}
        Assert.Equal(9, screen.StatusBars[0].ScreenRect.X);
        Assert.Equal(187, screen.StatusBars[0].ScreenRect.Y);
        Assert.Equal(304, screen.StatusBars[0].ScreenRect.Width);
        Assert.Equal(11, screen.StatusBars[0].ScreenRect.Height);
    }

    [Fact]
    public void TwoPlayerLayout_3PixelGapBetweenViews()
    {
        var screen = new Screen(RenderSize);
        var t1 = MakeTank(0, new Position(100, 100));
        var t2 = MakeTank(1, new Position(200, 100));
        screen.SetupTwoPlayers(t1, t2);

        int gap = screen.Views[1].ScreenRect.X - screen.Views[0].ScreenRect.Right;
        Assert.Equal(3, gap);
    }

    [Fact]
    public void Background_HasDottedPattern()
    {
        var screen = new Screen(RenderSize);
        var surface = new uint[RenderSize.Area];
        screen.FillBackground(surface, RenderSize.X, RenderSize.Y);

        uint bg = GuiColors.BackgroundArgb;
        uint dot = GuiColors.BackgroundDotArgb;

        // Row 0: dots at x=0, 4, 8, ...
        Assert.Equal(dot, surface[0]);
        Assert.Equal(bg, surface[1]);
        Assert.Equal(bg, surface[2]);
        Assert.Equal(bg, surface[3]);
        Assert.Equal(dot, surface[4]);

        // Row 1: dots at x=2, 6, 10, ...
        Assert.Equal(bg, surface[RenderSize.X]);
        Assert.Equal(bg, surface[RenderSize.X + 1]);
        Assert.Equal(dot, surface[RenderSize.X + 2]);
        Assert.Equal(bg, surface[RenderSize.X + 3]);
    }

    [Fact]
    public void GuiColors_MatchCppPalette()
    {
        Assert.Equal(new Color(0x00, 0x00, 0x00), GuiColors.Background);
        Assert.Equal(new Color(0x20, 0x20, 0x20), GuiColors.BackgroundDot);
        Assert.Equal(new Color(0x65, 0x65, 0x65), GuiColors.StatusBackground);
        Assert.Equal(new Color(0xF5, 0xEB, 0x1A), GuiColors.StatusEnergy);
        Assert.Equal(new Color(0x26, 0xF4, 0xF2), GuiColors.StatusHealth);
    }
}
