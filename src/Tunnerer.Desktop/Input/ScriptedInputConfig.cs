namespace Tunnerer.Desktop.Input;

public sealed class ScriptedInputConfig
{
    private readonly HashSet<int> _screenshotFrames = [];
    private readonly Dictionary<int, List<GameCommand>> _commandsByFrame = [];

    private ScriptedInputConfig() { }

    public ScriptedController? Controller { get; private init; }
    public string? RecordPath { get; private init; }
    public bool RecordAutoStart { get; private init; }
    public int ScreenshotFrameCount => _screenshotFrames.Count;
    public int CommandFrameCount => _commandsByFrame.Count;

    public static ScriptedInputConfig FromEnvironment()
    {
        var config = new ScriptedInputConfig
        {
            Controller = ScriptedController.TryParse(Environment.GetEnvironmentVariable("TUNNERER_SCRIPTED_INPUT")),
            RecordPath = NormalizeRecordPath(Environment.GetEnvironmentVariable("TUNNERER_RECORD_INPUT_PATH")),
            RecordAutoStart = IsTruthy(Environment.GetEnvironmentVariable("TUNNERER_RECORD_INPUT_AUTOSTART")),
        };

        int singleScreenshotFrame = ParseNonNegativeInt(
            Environment.GetEnvironmentVariable("TUNNERER_SCRIPT_SCREENSHOT_FRAME"), -1);
        if (singleScreenshotFrame >= 0)
            config._screenshotFrames.Add(singleScreenshotFrame);

        ParseScriptScreenshotFrames(
            Environment.GetEnvironmentVariable("TUNNERER_SCRIPT_SCREENSHOT_FRAMES"),
            config._screenshotFrames);
        ParseScriptCommands(
            Environment.GetEnvironmentVariable("TUNNERER_COMMAND_SCRIPT"),
            config._commandsByFrame);

        return config;
    }

    public bool HasScriptedController()
    {
        return Controller is not null;
    }

    public bool ShouldCaptureScreenshot(int frame)
    {
        return _screenshotFrames.Contains(frame);
    }

    public bool TryGetCommandsForFrame(int frame, out IReadOnlyList<GameCommand> commands)
    {
        if (_commandsByFrame.TryGetValue(frame, out List<GameCommand>? found))
        {
            commands = found;
            return true;
        }

        commands = Array.Empty<GameCommand>();
        return false;
    }

    private static int ParseNonNegativeInt(string? value, int fallback)
    {
        if (int.TryParse(value, out int parsed) && parsed >= 0)
            return parsed;
        return fallback;
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeRecordPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        string trimmed = raw.Trim();
        if (Path.IsPathRooted(trimmed))
            return trimmed;
        return Path.GetFullPath(trimmed, Directory.GetCurrentDirectory());
    }

    private static void ParseScriptScreenshotFrames(string? value, HashSet<int> targetFrames)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        string[] tokens = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (int.TryParse(tokens[i], out int frame) && frame >= 0)
                targetFrames.Add(frame);
        }
    }

    private static void ParseScriptCommands(string? value, Dictionary<int, List<GameCommand>> target)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        string[] entries = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            int split = entry.IndexOf(':');
            if (split < 0)
                split = entry.IndexOf('=');
            if (split <= 0 || split >= entry.Length - 1)
                continue;

            string frameToken = entry[..split].Trim();
            string commandToken = entry[(split + 1)..].Trim();
            if (!int.TryParse(frameToken, out int frame) || frame < 0)
                continue;
            if (!TryParseScriptCommand(commandToken, out GameCommand command))
                continue;

            if (!target.TryGetValue(frame, out List<GameCommand>? commands))
            {
                commands = [];
                target[frame] = commands;
            }
            commands.Add(command);
        }
    }

    private static bool TryParseScriptCommand(string token, out GameCommand command)
    {
        return Enum.TryParse(token.Trim(), ignoreCase: true, out command);
    }
}
