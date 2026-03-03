namespace Tunnerer.Desktop;

public class DrawProfile
{
    public TimeSpan TerrainDraw;
    public TimeSpan ObjectsDraw;
    public TimeSpan ScreenDraw;
    public TimeSpan ScreenHiResTerrain;
    public TimeSpan ScreenHiResEntities;
    public TimeSpan ScreenUpload;
    public TimeSpan ScreenBackendUpload;
    public TimeSpan ScreenTankGlowBuild;
    public TimeSpan ScreenAuxBuild;
    public TimeSpan ScreenQualityAdjust;
    public TimeSpan ScreenClearFrame;
    public TimeSpan ScreenNewFrame;
    public TimeSpan ScreenHudDraw;
    public TimeSpan ScreenUi;
    public TimeSpan ScreenImGuiRender;
    public TimeSpan ScreenSwap;
    public TimeSpan TotalFrame;
    public int FrameCount;

    public void Report()
    {
        if (FrameCount == 0) return;
        Console.WriteLine($"[Draw]    terrain={Avg(TerrainDraw):F3} objects={Avg(ObjectsDraw):F3} " +
            $"screen={Avg(ScreenDraw):F3} | total={Avg(TotalFrame):F3} ms (avg over {FrameCount} frames)");
        Console.WriteLine($"[Screen]  hiresTerrain={Avg(ScreenHiResTerrain):F3} hiresEntities={Avg(ScreenHiResEntities):F3} " +
            $"upload={Avg(ScreenUpload):F3} ui={Avg(ScreenUi):F3} imguiRender={Avg(ScreenImGuiRender):F3} " +
            $"swap={Avg(ScreenSwap):F3} ms");
        Console.WriteLine($"[Screen+] backendUpload={Avg(ScreenBackendUpload):F3} auxBuild={Avg(ScreenAuxBuild):F3} " +
            $"tankGlowBuild={Avg(ScreenTankGlowBuild):F3} " +
            $"qualityAdjust={Avg(ScreenQualityAdjust):F3} clear={Avg(ScreenClearFrame):F3} newFrame={Avg(ScreenNewFrame):F3} hud={Avg(ScreenHudDraw):F3} ms");
        Reset();
    }

    private double Avg(TimeSpan ts) => ts.TotalMilliseconds / FrameCount;

    public void Reset()
    {
        TerrainDraw = ObjectsDraw = ScreenDraw = TotalFrame = TimeSpan.Zero;
        ScreenHiResTerrain = ScreenHiResEntities = ScreenUpload = TimeSpan.Zero;
        ScreenBackendUpload = ScreenAuxBuild = ScreenTankGlowBuild = TimeSpan.Zero;
        ScreenQualityAdjust = ScreenClearFrame = ScreenNewFrame = ScreenHudDraw = TimeSpan.Zero;
        ScreenUi = ScreenImGuiRender = ScreenSwap = TimeSpan.Zero;
        FrameCount = 0;
    }
}
