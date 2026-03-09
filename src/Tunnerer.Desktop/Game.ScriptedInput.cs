namespace Tunnerer.Desktop.Input;

using Tunnerer.Core.Input;
using Tunnerer.Core.Types;

public sealed class ScriptedController
{
    private readonly Segment[] _segments;
    private int _currentSegmentIndex;
    private int _lastFrame = -1;

    private ScriptedController(Segment[] segments)
    {
        _segments = segments;
    }

    public static ScriptedController? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string[] items = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length == 0)
            return null;

        var segments = new List<Segment>(items.Length);
        int end = 0;
        foreach (string item in items)
        {
            string[] pair = item.Split(':', StringSplitOptions.TrimEntries);
            if (pair.Length != 2)
                return null;

            string[] move = pair[0].Split(',', StringSplitOptions.TrimEntries);
            bool shoot = false;
            if (move.Length != 2 && move.Length != 3 ||
                !int.TryParse(move[0], out int mx) ||
                !int.TryParse(move[1], out int my) ||
                !int.TryParse(pair[1], out int frames) ||
                frames <= 0)
            {
                return null;
            }
            if (move.Length == 3)
            {
                if (!int.TryParse(move[2], out int shootRaw))
                    return null;
                shoot = shootRaw != 0;
            }

            end += frames;
            segments.Add(new Segment(end, new Offset(mx, my), shoot));
        }

        return new ScriptedController(segments.ToArray());
    }

    public ControllerOutput GetOutputAtFrame(int frame)
    {
        if (_segments.Length == 0)
            return default;

        // Typical playback is monotonic by frame; keep a cursor
        // so we avoid scanning from the beginning every tick.
        if (frame < _lastFrame)
            _currentSegmentIndex = 0;
        _lastFrame = frame;

        while (_currentSegmentIndex < _segments.Length &&
               frame >= _segments[_currentSegmentIndex].EndFrameExclusive)
        {
            _currentSegmentIndex++;
        }

        if (_currentSegmentIndex >= _segments.Length)
            return default;

        ref readonly Segment segment = ref _segments[_currentSegmentIndex];
        if (frame < segment.EndFrameExclusive)
        {
            return new ControllerOutput
            {
                MoveSpeed = segment.Move,
                ShootPrimary = segment.Shoot,
            };
        }

        return default;
    }

    private readonly record struct Segment(int EndFrameExclusive, Offset Move, bool Shoot);
}
