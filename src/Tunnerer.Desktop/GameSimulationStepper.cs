namespace Tunnerer.Desktop;

using Tunnerer.Core;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Input;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Input;

public sealed class GameSimulationStepper
{
    private readonly World _world;
    private readonly KeyboardController _p1Controller;
    private readonly BotTankAI _p2AI;
    private readonly ScriptedInputConfig _scriptedInput;
    private readonly InputRecorder _inputRecorder;
    private readonly Action<GameCommand, string> _executeCommand;
    private readonly Action<string> _requestScreenshot;

    public GameSimulationStepper(
        World world,
        KeyboardController p1Controller,
        BotTankAI p2AI,
        ScriptedInputConfig scriptedInput,
        InputRecorder inputRecorder,
        Action<GameCommand, string> executeCommand,
        Action<string> requestScreenshot)
    {
        _world = world;
        _p1Controller = p1Controller;
        _p2AI = p2AI;
        _scriptedInput = scriptedInput;
        _inputRecorder = inputRecorder;
        _executeCommand = executeCommand;
        _requestScreenshot = requestScreenshot;
    }

    public SimulationStepResult AdvanceOneStep(int simFrame, IReadOnlyList<Tank> tanks, DirectionF? aimDirection, bool mouseShoot)
    {
        ApplyScriptCommandsForFrame(simFrame);

        ControllerOutput p1Output = default;
        _world.Advance(i =>
        {
            if (i == 0)
            {
                var kb = _p1Controller.Poll();
                ControllerOutput scripted = _scriptedInput.Controller?.GetOutputAtFrame(simFrame) ?? default;
                var move = _scriptedInput.Controller is null ? kb.MoveSpeed : scripted.MoveSpeed;
                p1Output = new ControllerOutput
                {
                    MoveSpeed = move,
                    ShootPrimary = kb.ShootPrimary || mouseShoot || scripted.ShootPrimary,
                    AimDirection = aimDirection ?? default,
                };
                return p1Output;
            }

            var enemy = tanks.Count > 0 ? tanks[0] : null;
            return _p2AI.GetInput(_world.TankList.Tanks[i], enemy, _world.Terrain);
        });

        _inputRecorder.RecordFrame(p1Output.MoveSpeed, p1Output.ShootPrimary);
        if (_scriptedInput.ShouldCaptureScreenshot(simFrame))
            _requestScreenshot($"script_frame_{simFrame:D4}");

        return new SimulationStepResult(IsGameOver: _world.IsGameOver);
    }

    private void ApplyScriptCommandsForFrame(int frame)
    {
        if (!_scriptedInput.TryGetCommandsForFrame(frame, out IReadOnlyList<GameCommand> commands))
            return;

        for (int i = 0; i < commands.Count; i++)
            _executeCommand(commands[i], InputCommandSources.Script);
    }
}

public readonly record struct SimulationStepResult(bool IsGameOver);
