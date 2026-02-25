namespace Tunnerer.Core;

using System.Diagnostics;

public class RepetitiveTimer
{
    private readonly TimeSpan _interval;
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private TimeSpan _nextTrigger;

    public RepetitiveTimer(TimeSpan interval)
    {
        _interval = interval;
        _nextTrigger = interval;
    }

    public bool AdvanceAndCheckElapsed()
    {
        if (_watch.Elapsed < _nextTrigger)
            return false;
        _nextTrigger += _interval;
        return true;
    }
}
