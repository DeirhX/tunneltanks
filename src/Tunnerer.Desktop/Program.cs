using Tunnerer.Core.LevelGen;
using Tunnerer.Core.Types;

Size? terrainSize = null;
bool fastGen = false;
bool perfMode = false;
int perfWarmupFrames = 180;
int perfMeasureFrames = 900;
string? perfCsvPath = null;
foreach (var arg in args)
{
    if (arg == "--large")
        terrainSize = new Size(1500, 750);
    else if (arg == "--fast")
        fastGen = true;
    else if (arg == "--perf")
        perfMode = true;
    else if (arg.StartsWith("--perf-warmup=") && int.TryParse(arg[14..], out int warmup))
        perfWarmupFrames = Math.Max(0, warmup);
    else if (arg.StartsWith("--perf-frames=") && int.TryParse(arg[14..], out int frames))
        perfMeasureFrames = Math.Max(1, frames);
    else if (arg.StartsWith("--perf-csv="))
        perfCsvPath = arg[11..];
    else if (arg.StartsWith("--size="))
    {
        var parts = arg[7..].Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            terrainSize = new Size(w, h);
    }
}

var mode = fastGen ? LevelGenMode.Optimized : LevelGenMode.Deterministic;
Tunnerer.Desktop.PerfCaptureOptions? perfOptions = perfMode
    ? new Tunnerer.Desktop.PerfCaptureOptions(perfWarmupFrames, perfMeasureFrames, perfCsvPath)
    : null;
var game = new Tunnerer.Desktop.Game(terrainSize, mode, perfOptions);
game.Run();


