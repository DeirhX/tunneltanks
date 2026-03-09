namespace Tunnerer.Desktop;

using System.Text;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Input;

public partial class Game
{
    private void ToggleInputRecording(string source)
    {
        if (_inputRecorder.IsRecording)
        {
            string path = ResolveRecordOutputPath();
            RecordingCapture capture = _inputRecorder.StopAndSerialize();
            if (string.IsNullOrWhiteSpace(capture.InputScript) && string.IsNullOrWhiteSpace(capture.CommandScript))
            {
                Console.WriteLine($"[Input] Scripted input recording stopped [source={source}] (no input/commands captured).");
                return;
            }

            SaveRecordedScript(path, capture);
            Console.WriteLine($"[Input] Scripted input recording stopped [source={source}] -> {path}");
            if (!string.IsNullOrWhiteSpace(capture.InputScript))
                Console.WriteLine($"[Input] Replay with TUNNERER_SCRIPTED_INPUT=\"{capture.InputScript}\"");
            if (!string.IsNullOrWhiteSpace(capture.CommandScript))
                Console.WriteLine($"[Input] Replay with TUNNERER_COMMAND_SCRIPT=\"{capture.CommandScript}\"");
            return;
        }

        _inputRecorder.Start();
        Console.WriteLine($"[Input] Scripted input recording started [source={source}] at frame {_simFrameCounter}.");
    }

    private void FlushInputRecordingOnExit()
    {
        if (!_inputRecorder.IsRecording)
            return;

        string path = ResolveRecordOutputPath();
        RecordingCapture capture = _inputRecorder.StopAndSerialize();
        if (string.IsNullOrWhiteSpace(capture.InputScript) && string.IsNullOrWhiteSpace(capture.CommandScript))
            return;

        SaveRecordedScript(path, capture);
        Console.WriteLine($"[Input] Scripted input recording auto-saved on exit -> {path}");
        if (!string.IsNullOrWhiteSpace(capture.InputScript))
            Console.WriteLine($"[Input] Replay with TUNNERER_SCRIPTED_INPUT=\"{capture.InputScript}\"");
        if (!string.IsNullOrWhiteSpace(capture.CommandScript))
            Console.WriteLine($"[Input] Replay with TUNNERER_COMMAND_SCRIPT=\"{capture.CommandScript}\"");
    }

    private string ResolveRecordOutputPath()
    {
        if (!string.IsNullOrWhiteSpace(_recordInputPath))
            return _recordInputPath;

        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "debug", $"scripted_input_{ts}.txt"));
    }

    private static void SaveRecordedScript(string path, RecordingCapture capture)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var text = new StringBuilder();
        text.AppendLine("# Scripted input recording");
        text.AppendLine($"# Generated: {DateTime.Now:O}");
        if (!string.IsNullOrWhiteSpace(capture.InputScript))
            text.AppendLine($"TUNNERER_SCRIPTED_INPUT={capture.InputScript}");
        if (!string.IsNullOrWhiteSpace(capture.CommandScript))
            text.AppendLine($"TUNNERER_COMMAND_SCRIPT={capture.CommandScript}");
        File.WriteAllText(path, text.ToString());
    }

    private void RecordInputCommandForReplay(GameCommand command, string source)
    {
        _inputRecorder.RecordCommand(_simFrameCounter, command, source);
    }

    private sealed class InputRecorder
    {
        private readonly List<Segment> _segments = [];
        private readonly List<CommandEntry> _commands = [];
        private bool _initialized;
        private Offset _currentMove;
        private bool _currentShoot;
        private int _currentFrames;

        public bool IsRecording { get; private set; }

        public void Start()
        {
            _segments.Clear();
            _commands.Clear();
            _initialized = false;
            _currentMove = default;
            _currentShoot = false;
            _currentFrames = 0;
            IsRecording = true;
        }

        public void RecordFrame(Offset move, bool shootPrimary)
        {
            if (!IsRecording)
                return;

            if (!_initialized)
            {
                _initialized = true;
                _currentMove = move;
                _currentShoot = shootPrimary;
                _currentFrames = 1;
                return;
            }

            if (_currentMove.X == move.X && _currentMove.Y == move.Y && _currentShoot == shootPrimary)
            {
                _currentFrames++;
                return;
            }

            FlushCurrent();
            _currentMove = move;
            _currentShoot = shootPrimary;
            _currentFrames = 1;
        }

        public RecordingCapture StopAndSerialize()
        {
            if (!IsRecording)
                return default;

            FlushCurrent();
            IsRecording = false;
            _initialized = false;
            _currentFrames = 0;

            var builder = new StringBuilder();
            for (int i = 0; i < _segments.Count; i++)
            {
                Segment s = _segments[i];
                if (i > 0)
                    builder.Append(';');
                builder.Append(s.Move.X)
                    .Append(',')
                    .Append(s.Move.Y)
                    .Append(',')
                    .Append(s.Shoot ? 1 : 0)
                    .Append(':')
                    .Append(s.Frames);
            }

            string commandScript = SerializeCommands();
            return new RecordingCapture(builder.ToString(), commandScript);
        }

        public void RecordCommand(int frame, GameCommand command, string source)
        {
            if (!IsRecording)
                return;
            if (string.Equals(source, "script", StringComparison.OrdinalIgnoreCase))
                return;
            if (command == GameCommand.ToggleInputRecording)
                return;

            _commands.Add(new CommandEntry(frame, command));
        }

        private void FlushCurrent()
        {
            if (!_initialized || _currentFrames <= 0)
                return;
            _segments.Add(new Segment(_currentMove, _currentShoot, _currentFrames));
        }

        private string SerializeCommands()
        {
            if (_commands.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            for (int i = 0; i < _commands.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(_commands[i].Frame)
                    .Append(':')
                    .Append(_commands[i].Command);
            }
            return builder.ToString();
        }

        private readonly record struct Segment(Offset Move, bool Shoot, int Frames);
        private readonly record struct CommandEntry(int Frame, GameCommand Command);
    }

    private readonly record struct RecordingCapture(string InputScript, string CommandScript);
}
