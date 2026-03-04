namespace Tunnerer.Core.Gui;

using Tunnerer.Core.Types;

public static class GuiColors
{
    public static readonly Color Background = new(0x00, 0x00, 0x00);
    public static readonly Color BackgroundDot = new(0x20, 0x20, 0x20);
    public static readonly Color StatusBackground = new(0x65, 0x65, 0x65);
    public static readonly Color StatusHeat = new(0xF5, 0xEB, 0x1A);
    public static readonly Color StatusHealth = new(0x26, 0xF4, 0xF2);
    public static readonly Color Blank = new(0x00, 0x00, 0x00);
    public static readonly Color ResourceInfoBackground = new(0x00, 0x00, 0x00, 0x80);
    public static readonly Color ResourceInfoOutline = new(0xFF, 0xFF, 0xFF, 0xA0);

    public static uint BackgroundArgb => Background.ToArgb();
    public static uint BackgroundDotArgb => BackgroundDot.ToArgb();
    public static uint StatusBackgroundArgb => StatusBackground.ToArgb();
    public static uint StatusHeatArgb => StatusHeat.ToArgb();
    public static uint StatusHealthArgb => StatusHealth.ToArgb();
    public static uint BlankArgb => Blank.ToArgb();
}
