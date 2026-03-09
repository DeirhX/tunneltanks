namespace Tunnerer.Desktop.Input;

public readonly record struct FrameInputSnapshot(int MouseX, int MouseY, uint MouseButtons)
{
    public bool IsLeftMouseDown => (MouseButtons & 1u) != 0;
}
