namespace Tunnerer.Core.Gui;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;

public class Screen
{
    private readonly Size _renderSize;
    private readonly List<TankView> _views = new();
    private readonly List<StatusBar> _statusBars = new();
    private readonly List<LivesDots> _livesDots = new();
    private readonly List<LetterBitmap> _letterBitmaps = new();
    private readonly List<ResourceOverlay> _resourceOverlays = new();

    private Position? _crosshairScreenPos;

    public IReadOnlyList<TankView> Views => _views;
    public IReadOnlyList<StatusBar> StatusBars => _statusBars;
    public IReadOnlyList<LivesDots> LivesDotsList => _livesDots;
    public IReadOnlyList<LetterBitmap> LetterBitmaps => _letterBitmaps;
    public IReadOnlyList<ResourceOverlay> ResourceOverlays => _resourceOverlays;

    public Screen(Size renderSize)
    {
        _renderSize = renderSize;
    }

    public void SetupSinglePlayer(Tank player)
    {
        ClearAll();
        int W = _renderSize.X, H = _renderSize.Y;

        var viewRect = new Rect(2, 2, W - 4, H - 4 - 2 - 11);
        _views.Add(new TankView(viewRect, player));

        var statusRect = new Rect(9, H - 2 - 11, W - 16, 11);
        _statusBars.Add(new StatusBar(statusRect, player, BarDirection.DecreasesToLeft));

        _letterBitmaps.Add(new LetterBitmap(3, statusRect.Y, 'T', GuiColors.StatusHeat));
        _letterBitmaps.Add(new LetterBitmap(3, statusRect.Y + 6, 'H', GuiColors.StatusHealth));

        _livesDots.Add(new LivesDots(statusRect.Right + 3, statusRect.Y + 1, player));

        _resourceOverlays.Add(new ResourceOverlay(new Rect(2, 2, 20, 20), player, HorizontalAlign.Left));
    }

    public void SetupTwoPlayers(Tank player1, Tank player2)
    {
        ClearAll();
        int W = _renderSize.X, H = _renderSize.Y;

        int viewW = (W - 4) / 2 - 1; // 157
        int viewH = H - 4 - 2 - 11;  // 183

        var view1 = new Rect(2, 2, viewW, viewH);
        var view2 = new Rect(view1.Right + 3, 2, viewW, viewH);
        _views.Add(new TankView(view1, player1));
        _views.Add(new TankView(view2, player2));

        int statusY = H - 2 - 11;
        int statusW = viewW - 2 - 2 - 2 - 1; // 150
        var status1 = new Rect(2, statusY, statusW, 11);
        var status2 = new Rect(status1.Right + 1 + 4 + 4 + 3 + 2 + 3, statusY, statusW, 11);
        _statusBars.Add(new StatusBar(status1, player1, BarDirection.DecreasesToRight));
        _statusBars.Add(new StatusBar(status2, player2, BarDirection.DecreasesToLeft));

        int centerX = W / 2 - 2;
        _letterBitmaps.Add(new LetterBitmap(centerX, statusY, 'T', GuiColors.StatusHeat));
        _letterBitmaps.Add(new LetterBitmap(centerX, statusY + 6, 'H', GuiColors.StatusHealth));

        _livesDots.Add(new LivesDots(status1.Right + 3, statusY + 1, player1));
        _livesDots.Add(new LivesDots(status2.Left - 4, statusY + 1, player2));

        _resourceOverlays.Add(new ResourceOverlay(new Rect(view1.X, view1.Y, 20, 20), player1, HorizontalAlign.Left));
        _resourceOverlays.Add(new ResourceOverlay(new Rect(view2.Right - 20, view2.Y, 20, 20), player2, HorizontalAlign.Right));
    }

    public void FillBackground(Surface surface)
    {
        uint bg = GuiColors.BackgroundArgb;
        uint dot = GuiColors.BackgroundDotArgb;
        Array.Fill(surface.Pixels, bg);
        for (int y = 0; y < surface.Height; y++)
            for (int x = (y % 2) * 2; x < surface.Width; x += 4)
                surface.Pixels[x + y * surface.Width] = dot;
    }

    /// <summary>
    /// Sets the crosshair screen position and returns the aim direction
    /// from the player's tank to the crosshair in world space.
    /// Returns null if the position isn't inside any view.
    /// </summary>
    public DirectionF? SetCrosshairScreenPos(int screenX, int screenY, int playerViewIndex = 0)
    {
        if (playerViewIndex < 0 || playerViewIndex >= _views.Count)
        { _crosshairScreenPos = null; return null; }

        var view = _views[playerViewIndex];
        int cx = Math.Clamp(screenX, view.ScreenRect.X, view.ScreenRect.Right - 1);
        int cy = Math.Clamp(screenY, view.ScreenRect.Y, view.ScreenRect.Bottom - 1);
        _crosshairScreenPos = new Position(cx, cy);

        var worldPos = view.ScreenToWorld(new Position(cx, cy));
        var tank = view.Tank;
        float dx = worldPos.X - tank.Position.X;
        float dy = worldPos.Y - tank.Position.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return null;
        return new DirectionF(dx / len, dy / len);
    }

    public void ClearCrosshair() => _crosshairScreenPos = null;

    public void Draw(Surface worldSurface, Surface screenSurface)
    {
        FillBackground(screenSurface);

        foreach (var view in _views)
            view.Draw(worldSurface, screenSurface);

        foreach (var bar in _statusBars)
            bar.Draw(screenSurface);

        foreach (var dots in _livesDots)
            dots.Draw(screenSurface);

        foreach (var letter in _letterBitmaps)
            letter.Draw(screenSurface);

        foreach (var overlay in _resourceOverlays)
            overlay.Draw(screenSurface);

        DrawCrosshair(screenSurface);
    }

    private void DrawCrosshair(Surface surface)
    {
        if (_crosshairScreenPos == null) return;
        int cx = _crosshairScreenPos.Value.X;
        int cy = _crosshairScreenPos.Value.Y;
        uint color = 0xFFFFFFFF; // white

        // 3x3 cross pattern: top, left, right, bottom (no center)
        SetPxSafe(surface, cx, cy - 1, color);
        SetPxSafe(surface, cx - 1, cy, color);
        SetPxSafe(surface, cx + 1, cy, color);
        SetPxSafe(surface, cx, cy + 1, color);
    }

    private static void SetPxSafe(Surface surface, int x, int y, uint color)
    {
        if (surface.IsInside(x, y))
            surface.Pixels[x + y * surface.Width] = color;
    }

    private void ClearAll()
    {
        _views.Clear();
        _statusBars.Clear();
        _livesDots.Clear();
        _letterBitmaps.Clear();
        _resourceOverlays.Clear();
    }
}

public enum HorizontalAlign { Left, Right }

public class LivesDots
{
    private readonly int _x, _y;
    private readonly Tank _tank;

    public LivesDots(int x, int y, Tank tank) { _x = x; _y = y; _tank = tank; }

    public void Draw(Surface surface)
    {
        uint active = GuiColors.StatusHealthArgb;
        uint inactive = GuiColors.BlankArgb;
        int yPos = 0;
        for (int life = 0; yPos + 2 <= 11; life++)
        {
            uint color = life < _tank.LivesLeft ? active : inactive;
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    int px = _x + dx, py = _y + yPos + dy;
                    if (surface.IsInside(px, py))
                        surface.Pixels[px + py * surface.Width] = color;
                }
            yPos += 3; // 2px dot + 1px spacing
        }
    }
}

public class LetterBitmap
{
    private readonly int _x, _y;
    private readonly byte[] _data;
    private readonly uint _color;

    private static readonly byte[] LetterE = { 1,1,1,1, 1,0,0,0, 1,1,1,0, 1,0,0,0, 1,1,1,1 };
    private static readonly byte[] LetterH = { 1,0,0,1, 1,0,0,1, 1,1,1,1, 1,0,0,1, 1,0,0,1 };
    private static readonly byte[] LetterT = { 1,1,1,1, 0,1,0,0, 0,1,0,0, 0,1,0,0, 0,1,0,0 };

    public LetterBitmap(int x, int y, char letter, Color color)
    {
        _x = x; _y = y;
        _data = letter switch
        {
            'E' => LetterE,
            'H' => LetterH,
            'T' => LetterT,
            _ => LetterH
        };
        _color = color.ToArgb();
    }

    public void Draw(Surface surface)
    {
        for (int gy = 0; gy < 5; gy++)
            for (int gx = 0; gx < 4; gx++)
            {
                if (_data[gx + gy * 4] == 0) continue;
                int px = _x + gx, py = _y + gy;
                if (surface.IsInside(px, py))
                    surface.Pixels[px + py * surface.Width] = _color;
            }
    }
}

public class ResourceOverlay
{
    private readonly Rect _rect;
    private readonly Tank _tank;
    private readonly HorizontalAlign _align;

    public ResourceOverlay(Rect rect, Tank tank, HorizontalAlign align)
    { _rect = rect; _tank = tank; _align = align; }

    public void Draw(Surface surface)
    {
        uint bgColor = new Color(0, 0, 0, 0x80).ToArgb();
        uint outlineColor = new Color(0xFF, 0xFF, 0xFF, 0xA0).ToArgb();

        // Draw outline
        for (int x = _rect.X; x < _rect.Right; x++)
        {
            SetPx(surface, x, _rect.Y, outlineColor);
            SetPx(surface, x, _rect.Bottom - 1, outlineColor);
        }
        for (int y = _rect.Y + 1; y < _rect.Bottom - 1; y++)
        {
            SetPx(surface, _rect.X, y, outlineColor);
            SetPx(surface, _rect.Right - 1, y, outlineColor);
        }
        // Fill interior
        for (int y = _rect.Y + 1; y < _rect.Bottom - 1; y++)
            for (int x = _rect.X + 1; x < _rect.Right - 1; x++)
                SetPx(surface, x, y, bgColor);

        string dirtText = (_tank.Resources.Dirt / 10).ToString();
        string baseDirt = (_tank.Base?.Materials.Dirt ?? 0).ToString();

        int textX = _rect.X + 2;
        FontRenderer.DrawString(surface, textX, _rect.Y + 2, dirtText, GuiColors.StatusHeat);
        FontRenderer.DrawString(surface, textX, _rect.Y + 11, baseDirt, GuiColors.StatusHealth);
    }

    private static void SetPx(Surface surface, int x, int y, uint color)
    {
        if (surface.IsInside(x, y))
            surface.Pixels[x + y * surface.Width] = color;
    }
}
