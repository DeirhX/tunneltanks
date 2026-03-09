namespace Tunnerer.Desktop;

public sealed class GameLoopScheduler
{
    private readonly TimeSpan _step;
    private readonly int _maxCatchUpSteps;
    private TimeSpan _accumulator;

    public GameLoopScheduler(TimeSpan step, int maxCatchUpSteps)
    {
        _step = step;
        _maxCatchUpSteps = Math.Max(1, maxCatchUpSteps);
    }

    public bool HasReadyStep => _accumulator >= _step;

    public void AddElapsed(TimeSpan elapsed)
    {
        _accumulator += elapsed;
    }

    public int ConsumeReadySteps(Action onStep, Func<bool> canContinue)
    {
        int executed = 0;
        while (_accumulator >= _step && executed < _maxCatchUpSteps && canContinue())
        {
            _accumulator -= _step;
            executed++;
            onStep();
        }

        if (executed == _maxCatchUpSteps && _accumulator > _step)
            _accumulator = _step;

        return executed;
    }
}
