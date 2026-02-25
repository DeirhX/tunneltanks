namespace TunnelTanks.Desktop.Gui;

using System.Numerics;
using ImGuiNET;
using TunnelTanks.Core;
using TunnelTanks.Core.Entities;

public class GameHud
{
    public const float BottomPanelHeight = 223f;

    private nint _energyIconTex, _shieldIconTex;
    private nint _panelFrameTex, _buildPanelTex;
    private nint _digitStripTex;

    private const int DigitCount = 10;
    private const int DigitCellW = 17;
    private const int DigitCellH = 20;
    private const int DigitStripW = DigitCellW * DigitCount; // 170
    private const float DigitSpacing = 1f;

    public (float x, float y, float w, float h) ViewportRect { get; private set; }
    public (float x, float y)? CrosshairScreenPos { get; set; }

    public void Init(nint energyIcon, nint shieldIcon, nint panelFrame, nint buildPanel, nint digitStrip)
    {
        _energyIconTex = energyIcon;
        _shieldIconTex = shieldIcon;
        _panelFrameTex = panelFrame;
        _buildPanelTex = buildPanel;
        _digitStripTex = digitStrip;
    }

    public void Draw(nint gameTextureId, int texW, int texH, Tank player, World world, float deltaTime)
    {
        _ = world;
        _ = deltaTime;
        DrawGameViewport(gameTextureId, texW, texH);
        DrawCrosshair();
        DrawBottomPanel(player);
    }

    private void DrawGameViewport(nint gameTextureId, int texW, int texH)
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        float viewW = displaySize.X;
        float viewH = displaySize.Y;

        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(viewW, viewH), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);

        if (ImGui.Begin("##Viewport", flags))
        {
            var avail = ImGui.GetContentRegionAvail();
            float aspect = (float)texW / texH;
            float imgW, imgH;
            if (avail.X / avail.Y > aspect)
            {
                imgH = avail.Y;
                imgW = imgH * aspect;
            }
            else
            {
                imgW = avail.X;
                imgH = imgW / aspect;
            }

            float offsetX = (avail.X - imgW) * 0.5f;
            float offsetY = (avail.Y - imgH) * 0.5f;
            var cursorPos = ImGui.GetCursorScreenPos();

            ImGui.SetCursorPos(new Vector2(offsetX, offsetY));
            ImGui.Image(gameTextureId, new Vector2(imgW, imgH));
            ViewportRect = (cursorPos.X + offsetX, cursorPos.Y + offsetY, imgW, imgH);
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private void DrawCrosshair()
    {
        if (CrosshairScreenPos is not { } pos) return;
        var vp = ViewportRect;
        if (pos.x < vp.x || pos.y < vp.y || pos.x >= vp.x + vp.w || pos.y >= vp.y + vp.h) return;

        var dl = ImGui.GetForegroundDrawList();
        const float armLen = 10f;
        const float gap = 3f;
        const uint col = 0xFFFFFFFF;
        dl.AddLine(new Vector2(pos.x - armLen, pos.y), new Vector2(pos.x - gap, pos.y), col, 2f);
        dl.AddLine(new Vector2(pos.x + gap, pos.y), new Vector2(pos.x + armLen, pos.y), col, 2f);
        dl.AddLine(new Vector2(pos.x, pos.y - armLen), new Vector2(pos.x, pos.y - gap), col, 2f);
        dl.AddLine(new Vector2(pos.x, pos.y + gap), new Vector2(pos.x, pos.y + armLen), col, 2f);
    }

    private void DrawBottomPanel(Tank player)
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        var dl = ImGui.GetForegroundDrawList();

        const float eW = 245f, sW = 145f, mW = 230f, bW = 404f;
        const float h = 223f;
        const float stripW = eW + sW + mW + bW; // 1024

        float startX = MathF.Floor((displaySize.X - stripW) * 0.5f);
        float drawY = displaySize.Y - h;

        float x = startX;
        dl.AddImage(_energyIconTex, new Vector2(x, drawY), new Vector2(x + eW, drawY + h));
        x += eW;
        dl.AddImage(_shieldIconTex, new Vector2(x, drawY), new Vector2(x + sW, drawY + h));
        x += sW;
        dl.AddImage(_panelFrameTex, new Vector2(x, drawY), new Vector2(x + mW, drawY + h));
        x += mW;
        dl.AddImage(_buildPanelTex, new Vector2(x, drawY), new Vector2(x + bW, drawY + h));

        // Energy value: inside the green label plate below "ENERGY" text
        int energy = player.Reactor.Energy;
        float plateX = startX + 105f;
        float plateW = 130f;
        float plateY = drawY + 168f;
        DrawNumberCentered(dl, energy, plateX, plateY, plateW);

        // Shield/health value: inside the blue label plate below "SHIELD" text
        int health = player.Reactor.Health;
        float sPlateX = startX + eW + 10f;
        float sPlateW = 125f;
        float sPlateY = drawY + 166f;
        DrawNumberCentered(dl, health, sPlateX, sPlateY, sPlateW);
    }

    private void DrawNumberCentered(ImDrawListPtr dl, int value, float areaX, float areaY, float areaW)
    {
        string text = value.ToString();
        float totalW = text.Length > 0
            ? text.Length * DigitCellW + (text.Length - 1) * DigitSpacing
            : 0f;
        float x = MathF.Floor(areaX + (areaW - totalW) * 0.5f);
        float y = MathF.Floor(areaY);

        foreach (char c in text)
        {
            if (c is >= '0' and <= '9')
            {
                int idx = c - '0';
                // Half-texel inset prevents sampling bleed from neighbor digits.
                float texelU = 0.5f / DigitStripW;
                float u0 = (float)(idx * DigitCellW) / DigitStripW + texelU;
                float u1 = (float)((idx + 1) * DigitCellW) / DigitStripW - texelU;
                dl.AddImage(_digitStripTex,
                    new Vector2(x, y),
                    new Vector2(x + DigitCellW, y + DigitCellH),
                    new Vector2(u0, 0f),
                    new Vector2(u1, 1f));
            }
            x += DigitCellW + DigitSpacing;
        }
    }

    private static uint ToAbgr(float r, float g, float b, float a)
    {
        byte rb = (byte)(MathF.Min(r, 1f) * 255 + 0.5f);
        byte gb = (byte)(MathF.Min(g, 1f) * 255 + 0.5f);
        byte bb = (byte)(MathF.Min(b, 1f) * 255 + 0.5f);
        byte ab = (byte)(MathF.Min(a, 1f) * 255 + 0.5f);
        return (uint)(ab << 24 | bb << 16 | gb << 8 | rb);
    }
}
