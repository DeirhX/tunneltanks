namespace Tunnerer.Core.Gui;

using Tunnerer.Core.Types;
using Tunnerer.Core.Entities;

public enum BarDirection { DecreasesToRight, DecreasesToLeft }

public class StatusBar
{
    public Rect ScreenRect { get; }
    private readonly Tank _tank;
    private readonly bool _decreasesToLeft;

    private const int Border = 1;

    public StatusBar(Rect screenRect, Tank tank, BarDirection direction)
    {
        ScreenRect = screenRect;
        _tank = tank;
        _decreasesToLeft = direction == BarDirection.DecreasesToLeft;
    }

    public void Draw(Surface surface)
    {
        int w = ScreenRect.Width;
        int h = ScreenRect.Height;
        int midY = (h - 1) / 2;
        int midH = (h % 2 != 0) ? 1 : 2;

        int energyFilled = _tank.Reactor.EnergyCapacity > 0
            ? _tank.Reactor.Energy * (w - Border * 2) / _tank.Reactor.EnergyCapacity
            : 0;
        int healthFilled = _tank.Reactor.HealthCapacity > 0
            ? _tank.Reactor.Health * (w - Border * 2) / _tank.Reactor.HealthCapacity
            : 0;

        if (!_decreasesToLeft)
        {
            energyFilled = w - Border - energyFilled;
            healthFilled = w - Border - healthFilled;
        }
        else
        {
            energyFilled += Border;
            healthFilled += Border;
        }

        uint bgColor = GuiColors.StatusBackgroundArgb;
        uint energyColor = GuiColors.StatusEnergyArgb;
        uint healthColor = GuiColors.StatusHealthArgb;
        uint blankColor = GuiColors.BlankArgb;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int sx = ScreenRect.X + x;
                int sy = ScreenRect.Y + y;
                if (sx < 0 || sy < 0 || sx >= surface.Width) continue;

                // Rounded outer corners
                if ((x == 0 || x == w - 1) && (y == 0 || y == h - 1))
                    continue;

                uint c;

                // Outer border
                if (y < Border || y >= h - Border || x < Border || x >= w - Border)
                    c = bgColor;
                // Rounded inner corners
                else if ((x == Border || x == w - Border - 1) && (y == Border || y == h - Border - 1))
                    c = bgColor;
                // Middle separator
                else if (y >= midY && y < midY + midH)
                    c = bgColor;
                // Energy bar (top half)
                else if (y < midY && ((_decreasesToLeft && x < energyFilled) || (!_decreasesToLeft && x >= energyFilled)))
                    c = energyColor;
                // Health bar (bottom half)
                else if (y > midY && ((_decreasesToLeft && x < healthFilled) || (!_decreasesToLeft && x >= healthFilled)))
                    c = healthColor;
                // Empty bar portion
                else
                    c = blankColor;

                surface.Pixels[sx + sy * surface.Width] = c;
            }
        }
    }
}
