namespace TunnelTanks.Desktop.Gui;

using System.Numerics;
using ImGuiNET;
using TunnelTanks.Core;
using TunnelTanks.Core.Entities;

/// <summary>
/// Bitmap-only HUD composition based on extracted reference sprites.
/// </summary>
public class GameHud
{
    public const float BottomPanelHeight = 320f;
    private static readonly uint PanelBgU = ToAbgr(0.05f, 0.05f, 0.07f, 0.96f);
    private static readonly uint PanelTopLineU = ToAbgr(0.40f, 0.50f, 0.60f, 0.50f);

    private nint _energyIconTex, _shieldIconTex;
    private nint _panelFrameTex, _buildPanelTex;

    public (float x, float y, float w, float h) ViewportRect { get; private set; }
    public (float x, float y)? CrosshairScreenPos { get; set; }

    public void Init(nint energyIcon, nint shieldIcon, nint panelFrame, nint buildPanel)
    {
        _energyIconTex = energyIcon;
        _shieldIconTex = shieldIcon;
        _panelFrameTex = panelFrame;
        _buildPanelTex = buildPanel;
    }

    public void Draw(nint gameTextureId, int texW, int texH, Tank player, World world, float deltaTime)
    {
        _ = player;
        _ = world;
        _ = deltaTime;
        DrawGameViewport(gameTextureId, texW, texH);
        DrawCrosshair();
        DrawBottomPanel();
    }

    private void DrawGameViewport(nint gameTextureId, int texW, int texH)
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        float viewW = displaySize.X;
        float viewH = displaySize.Y - BottomPanelHeight;

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

    private void DrawBottomPanel()
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        float panelY = displaySize.Y - BottomPanelHeight;

        ImGui.SetNextWindowPos(new Vector2(0, panelY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(displaySize.X, BottomPanelHeight), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);

        if (ImGui.Begin("##BottomPanel", flags))
        {
            var dl = ImGui.GetWindowDrawList();
            float totalW = displaySize.X;
            dl.AddRectFilled(new Vector2(0, panelY), new Vector2(totalW, displaySize.Y), PanelBgU);
            dl.AddLine(new Vector2(0, panelY), new Vector2(totalW, panelY), PanelTopLineU, 2f);

            // Source pixel widths (all 486px tall from same Y band):
            const float srcEnergyW = 245f;
            const float srcShieldW = 145f;
            const float srcMatW = 230f;
            const float srcBuildW = 404f;
            const float srcH = 486f;
            const float srcTotalW = srcEnergyW + srcShieldW + srcMatW + srcBuildW; // 1024

            // Scale uniformly so the strip height fits the panel (with small margin)
            float scale = (BottomPanelHeight - 8f) / srcH;

            float contentW = srcTotalW * scale;
            float startX = (totalW - contentW) * 0.5f;
            float drawY = panelY + 4f;
            float drawH = srcH * scale;

            float x = startX;
            float eW = srcEnergyW * scale;
            float sW = srcShieldW * scale;
            float mW = srcMatW * scale;
            float bW = srcBuildW * scale;

            dl.AddImage(_energyIconTex, new Vector2(x, drawY), new Vector2(x + eW, drawY + drawH));
            x += eW;
            dl.AddImage(_shieldIconTex, new Vector2(x, drawY), new Vector2(x + sW, drawY + drawH));
            x += sW;
            dl.AddImage(_panelFrameTex, new Vector2(x, drawY), new Vector2(x + mW, drawY + drawH));
            x += mW;
            dl.AddImage(_buildPanelTex, new Vector2(x, drawY), new Vector2(x + bW, drawY + drawH));
        }

        ImGui.End();
        ImGui.PopStyleVar(2);
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
