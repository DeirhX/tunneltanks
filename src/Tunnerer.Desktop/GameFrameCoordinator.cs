namespace Tunnerer.Desktop;

using System.Diagnostics;
using Tunnerer.Desktop.Input;

public sealed class GameFrameCoordinator
{
    private readonly GameLoopScheduler _loopScheduler;
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();

    public GameFrameCoordinator(TimeSpan targetFrameTime, int maxCatchUpSteps)
    {
        _loopScheduler = new GameLoopScheduler(targetFrameTime, maxCatchUpSteps);
    }

    public void Run(
        Func<bool> isRunning,
        Action requestStop,
        Func<bool> pollEvents,
        Func<bool> isGameOver,
        Action onBeforeSimulationBatch,
        Func<FrameInputSnapshot> captureFrameInput,
        Action<FrameInputSnapshot> advanceOneSimulationStep,
        Action composeFrame,
        Action<FrameInputSnapshot> renderFrame,
        Action<TimeSpan> onFrameMeasured)
    {
        while (isRunning())
        {
            _loopScheduler.AddElapsed(_frameTimer.Elapsed);
            _frameTimer.Restart();

            if (!pollEvents())
            {
                requestStop();
                break;
            }

            if (isGameOver())
            {
                requestStop();
                break;
            }

            if (!_loopScheduler.HasReadyStep)
                continue;

            var totalFrameWatch = Stopwatch.StartNew();
            onBeforeSimulationBatch();
            FrameInputSnapshot frameInput = captureFrameInput();
            _loopScheduler.ConsumeReadySteps(
                onStep: () => advanceOneSimulationStep(frameInput),
                canContinue: isRunning);
            composeFrame();
            renderFrame(frameInput);
            onFrameMeasured(totalFrameWatch.Elapsed);
        }
    }
}
