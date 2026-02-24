namespace TunnelTanks.Core.Gui;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Entities;

public class TankView
{
    public Rect ScreenRect { get; }
    public Tank Tank => _tank;
    private readonly Tank _tank;
    private int _staticCounter;

    public TankView(Rect screenRect, Tank tank)
    {
        ScreenRect = screenRect;
        _tank = tank;
    }

    public Position WorldToScreen(Position worldPos) =>
        new(worldPos.X - _tank.Position.X + ScreenRect.X + ScreenRect.Width / 2,
            worldPos.Y - _tank.Position.Y + ScreenRect.Y + ScreenRect.Height / 2);

    public Position ScreenToWorld(Position screenPos) =>
        new(screenPos.X - ScreenRect.X - ScreenRect.Width / 2 + _tank.Position.X,
            screenPos.Y - ScreenRect.Y - ScreenRect.Height / 2 + _tank.Position.Y);

    public void Draw(uint[] worldSurface, int worldW, int worldH, uint[] screenSurface, int screenW)
    {
        int halfW = ScreenRect.Width / 2;
        int halfH = ScreenRect.Height / 2;
        int centerX = _tank.Position.X;
        int centerY = _tank.Position.Y;

        for (int sy = 0; sy < ScreenRect.Height; sy++)
        {
            int worldY = centerY - halfH + sy;
            int screenY = ScreenRect.Y + sy;
            if (screenY < 0) continue;

            for (int sx = 0; sx < ScreenRect.Width; sx++)
            {
                int worldX = centerX - halfW + sx;
                int screenX = ScreenRect.X + sx;
                if (screenX < 0) continue;

                uint pixel;
                if (worldX >= 0 && worldX < worldW && worldY >= 0 && worldY < worldH)
                    pixel = worldSurface[worldX + worldY * worldW];
                else
                    pixel = 0xFF505050;

                screenSurface[screenX + screenY * screenW] = pixel;
            }
        }

        if (_tank.IsDead)
            DrawStatic(screenSurface, screenW);
    }

    private void DrawStatic(uint[] screenSurface, int screenW)
    {
        _staticCounter++;
        var rng = new Random(_staticCounter);
        for (int sy = 0; sy < ScreenRect.Height; sy++)
        {
            int screenY = ScreenRect.Y + sy;
            if (rng.Next(1000) < 500) continue;
            for (int sx = 0; sx < ScreenRect.Width; sx++)
            {
                int screenX = ScreenRect.X + sx;
                if (rng.Next(1000) < 700)
                    screenSurface[screenX + screenY * screenW] = 0xFF000000;
            }
        }
    }
}
