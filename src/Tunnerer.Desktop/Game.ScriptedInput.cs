namespace Tunnerer.Desktop;

using Tunnerer.Core.Types;

public partial class Game
{
    private sealed class ScriptedController
    {
        private readonly Segment[] _segments;

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
                if (move.Length != 2 ||
                    !int.TryParse(move[0], out int mx) ||
                    !int.TryParse(move[1], out int my) ||
                    !int.TryParse(pair[1], out int frames) ||
                    frames <= 0)
                {
                    return null;
                }

                end += frames;
                segments.Add(new Segment(end, new Offset(mx, my)));
            }

            return new ScriptedController(segments.ToArray());
        }

        public Offset GetMoveAtFrame(int frame)
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                if (frame < _segments[i].EndFrameExclusive)
                    return _segments[i].Move;
            }

            return new Offset(0, 0);
        }

        private readonly record struct Segment(int EndFrameExclusive, Offset Move);
    }
}
