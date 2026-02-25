using Tunnerer.Core.LevelGen;
using Tunnerer.Core.Types;

Size? terrainSize = null;
bool fastGen = false;
foreach (var arg in args)
{
    if (arg == "--large")
        terrainSize = new Size(1500, 750);
    else if (arg == "--fast")
        fastGen = true;
    else if (arg.StartsWith("--size="))
    {
        var parts = arg[7..].Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            terrainSize = new Size(w, h);
    }
}

var mode = fastGen ? LevelGenMode.Optimized : LevelGenMode.Deterministic;
var game = new Tunnerer.Desktop.Game(terrainSize, mode);
game.Run();


