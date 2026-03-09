namespace Tunnerer.Desktop.Input;

using Silk.NET.SDL;
using Tunnerer.Desktop.Rendering;

public sealed class GameCommandController
{
    private bool _showHeatDebugOverlay;
    private bool _showPostPassOverlay = true;
    private PostProcessPassFlags _enabledPostPasses = PostProcessPassFlags.All;

    public bool ShowHeatDebugOverlay => _showHeatDebugOverlay;
    public bool ShowPostPassOverlay => _showPostPassOverlay;
    public PostProcessPassFlags EnabledPostPasses => _enabledPostPasses;

    public static bool TryTranslateHotkey(Scancode scancode, out GameCommand command)
    {
        switch (scancode)
        {
            case Scancode.ScancodeF9:
                command = GameCommand.ToggleHeatOverlay;
                return true;
            case Scancode.Scancode0:
                command = GameCommand.ToggleHeatOverlay;
                return true;
            case Scancode.Scancode9:
                command = GameCommand.ToggleThermalRegionsOverlay;
                return true;
            case Scancode.Scancode1:
                command = GameCommand.TogglePostVignette;
                return true;
            case Scancode.Scancode2:
                command = GameCommand.TogglePostTerrainCurve;
                return true;
            case Scancode.Scancode3:
                command = GameCommand.TogglePostTerrainAux;
                return true;
            case Scancode.Scancode4:
                command = GameCommand.TogglePostTankGlow;
                return true;
            case Scancode.Scancode5:
                command = GameCommand.TogglePostNativeTerrainSmoothing;
                return true;
            case Scancode.Scancode6:
                command = GameCommand.TogglePostTerrainHeat;
                return true;
            case Scancode.Scancode7:
                command = GameCommand.TogglePostNativeEdgeCurving;
                return true;
            case Scancode.ScancodeF10:
                command = GameCommand.RequestScreenshot;
                return true;
            case Scancode.ScancodeF11:
                command = GameCommand.TogglePostPassOverlay;
                return true;
            case Scancode.ScancodeF12:
                command = GameCommand.ToggleInputRecording;
                return true;
            default:
                command = default;
                return false;
        }
    }

    public void Execute(
        GameCommand command,
        string source,
        Action<string?> requestScreenshot,
        Action<string> toggleInputRecording)
    {
        switch (command)
        {
            case GameCommand.ToggleThermalRegionsOverlay:
                TogglePassWithLog(PostProcessPassFlags.ThermalRegions, "Thermal regions", source);
                return;
            case GameCommand.ToggleHeatOverlay:
                ToggleWithLog(ref _showHeatDebugOverlay, "Debug", "Heat overlay", source);
                return;
            case GameCommand.TogglePostVignette:
                TogglePassWithLog(PostProcessPassFlags.Vignette, "Vignette", source);
                return;
            case GameCommand.TogglePostTerrainCurve:
                TogglePassWithLog(PostProcessPassFlags.TerrainCurve, "Terrain curve", source);
                return;
            case GameCommand.TogglePostTerrainAux:
                TogglePassWithLog(PostProcessPassFlags.TerrainAux, "Terrain texture", source);
                return;
            case GameCommand.TogglePostTerrainHeat:
                TogglePassWithLog(PostProcessPassFlags.TerrainHeat, "Terrain heat", source);
                return;
            case GameCommand.TogglePostTankGlow:
                TogglePassWithLog(PostProcessPassFlags.TankGlow, "Tank glow", source);
                return;
            case GameCommand.TogglePostNativeTerrainSmoothing:
                TogglePassWithLog(PostProcessPassFlags.NativeTerrainSmoothing, "Native terrain smoothing", source);
                return;
            case GameCommand.TogglePostNativeEdgeCurving:
                TogglePassWithLog(PostProcessPassFlags.NativeEdgeCurving, "Native edge curving", source);
                return;
            case GameCommand.RequestScreenshot:
                requestScreenshot("postps");
                Console.WriteLine($"[Debug] Screenshot requested [source={source}].");
                return;
            case GameCommand.TogglePostPassOverlay:
                ToggleWithLog(ref _showPostPassOverlay, "Debug", "Post pass overlay", source);
                return;
            case GameCommand.ToggleInputRecording:
                toggleInputRecording(source);
                return;
            default:
                return;
        }
    }

    private static void ToggleWithLog(ref bool flag, string group, string label, string source)
    {
        flag = !flag;
        Console.WriteLine($"[{group}] {label}: {(flag ? "ON" : "OFF")} [source={source}]");
    }

    private void TogglePassWithLog(PostProcessPassFlags pass, string label, string source)
    {
        _enabledPostPasses ^= pass;
        bool enabled = (_enabledPostPasses & pass) != 0;
        Console.WriteLine($"[PostPs] {label}: {(enabled ? "ON" : "OFF")} [source={source}]");
    }
}
