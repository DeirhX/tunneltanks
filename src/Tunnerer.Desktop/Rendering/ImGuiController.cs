namespace Tunnerer.Desktop.Rendering;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using GL_PixelFormat = Silk.NET.OpenGL.PixelFormat;
using GL_PixelType = Silk.NET.OpenGL.PixelType;

/// <summary>
/// SDL2 + OpenGL3.3 Dear ImGui bridge (input + UI draw only).
/// Game-frame post-processing is handled by backend-specific renderers.
/// </summary>
public sealed unsafe class ImGuiController : IDisposable
{
    private readonly GL _gl;
    private readonly Sdl _sdl;
    private readonly Window* _window;

    private uint _fontTexture;
    private uint _shaderProgram;
    private int _attribLocTex;
    private int _attribLocProjMtx;
    private uint _attribLocVtxPos;
    private uint _attribLocVtxUV;
    private uint _attribLocVtxColor;
    private uint _vboHandle;
    private uint _eboHandle;
    private uint _vaoHandle;
    private bool _disposed;

    public ImGuiController(GL gl, Sdl sdl, Window* window, int windowW, int windowH)
    {
        _gl = gl;
        _sdl = sdl;
        _window = window;

        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        io.DisplaySize = new Vector2(windowW, windowH);
        io.DisplayFramebufferScale = Vector2.One;

        ImGui.StyleColorsDark();
        SetupKeyMap();
        CreateDeviceObjects();

    }

    public void ProcessEvent(Event ev)
    {
        var io = ImGui.GetIO();

        switch ((EventType)ev.Type)
        {
            case EventType.Mousemotion:
                io.AddMousePosEvent(ev.Motion.X, ev.Motion.Y);
                break;
            case EventType.Mousebuttondown:
            case EventType.Mousebuttonup:
            {
                int button = ev.Button.Button switch
                {
                    1 => 0, // SDL_BUTTON_LEFT
                    2 => 2, // SDL_BUTTON_MIDDLE
                    3 => 1, // SDL_BUTTON_RIGHT
                    _ => -1
                };
                if (button >= 0)
                    io.AddMouseButtonEvent(button, ev.Type == (uint)EventType.Mousebuttondown);
                break;
            }
            case EventType.Mousewheel:
                io.AddMouseWheelEvent(ev.Wheel.X, ev.Wheel.Y);
                break;
            case EventType.Textinput:
            {
                string text = Marshal.PtrToStringUTF8((nint)ev.Text.Text) ?? "";
                foreach (char c in text)
                    io.AddInputCharacter(c);
                break;
            }
            case EventType.Keydown:
            case EventType.Keyup:
            {
                bool down = ev.Type == (uint)EventType.Keydown;
                var scancode = (Scancode)ev.Key.Keysym.Scancode;
                var key = SdlScancodeToImGuiKey(scancode);
                if (key != ImGuiKey.None)
                    io.AddKeyEvent(key, down);

                io.AddKeyEvent(ImGuiKey.ModCtrl, (ev.Key.Keysym.Mod & (ushort)Keymod.Ctrl) != 0);
                io.AddKeyEvent(ImGuiKey.ModShift, (ev.Key.Keysym.Mod & (ushort)Keymod.Shift) != 0);
                io.AddKeyEvent(ImGuiKey.ModAlt, (ev.Key.Keysym.Mod & (ushort)Keymod.Alt) != 0);
                break;
            }
            case EventType.Windowevent:
                break;
        }
    }

    public void NewFrame(int windowW, int windowH, float deltaTime)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(windowW, windowH);

        int fbW, fbH;
        _sdl.GLGetDrawableSize(_window, &fbW, &fbH);
        if (windowW > 0 && windowH > 0)
            io.DisplayFramebufferScale = new Vector2((float)fbW / windowW, (float)fbH / windowH);

        io.DeltaTime = deltaTime > 0 ? deltaTime : 1f / 60f;

        ImGui.NewFrame();
    }

    public void Render()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    private void CreateDeviceObjects()
    {
        CreateFontTexture();
        CreateShaderProgram();

        _attribLocTex = _gl.GetUniformLocation(_shaderProgram, "Texture");
        _attribLocProjMtx = _gl.GetUniformLocation(_shaderProgram, "ProjMtx");
        _attribLocVtxPos = (uint)_gl.GetAttribLocation(_shaderProgram, "Position");
        _attribLocVtxUV = (uint)_gl.GetAttribLocation(_shaderProgram, "UV");
        _attribLocVtxColor = (uint)_gl.GetAttribLocation(_shaderProgram, "Color");

        _vboHandle = _gl.GenBuffer();
        _eboHandle = _gl.GenBuffer();
        _vaoHandle = _gl.GenVertexArray();

        _gl.BindVertexArray(_vaoHandle);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboHandle);

        int stride = Unsafe.SizeOf<ImDrawVert>();
        _gl.EnableVertexAttribArray(_attribLocVtxPos);
        _gl.VertexAttribPointer(_attribLocVtxPos, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
        _gl.EnableVertexAttribArray(_attribLocVtxUV);
        _gl.VertexAttribPointer(_attribLocVtxUV, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)8);
        _gl.EnableVertexAttribArray(_attribLocVtxColor);
        _gl.VertexAttribPointer(_attribLocVtxColor, 4, VertexAttribPointerType.UnsignedByte, true, (uint)stride, (void*)16);

        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    private void CreateFontTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out _);

        _fontTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _fontTexture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
            (uint)width, (uint)height, 0, GL_PixelFormat.Rgba, GL_PixelType.UnsignedByte, pixels);

        io.Fonts.SetTexID((nint)_fontTexture);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void CreateShaderProgram()
    {
        const string vertSrc = @"#version 330 core
layout (location = 0) in vec2 Position;
layout (location = 1) in vec2 UV;
layout (location = 2) in vec4 Color;
uniform mat4 ProjMtx;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main() {
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position.xy, 0.0, 1.0);
}
";
        const string fragSrc = @"#version 330 core
in vec2 Frag_UV;
in vec4 Frag_Color;
uniform sampler2D Texture;
layout (location = 0) out vec4 Out_Color;
void main() {
    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
}
";
        uint vs = CompileShader(ShaderType.VertexShader, vertSrc);
        uint fs = CompileShader(ShaderType.FragmentShader, fragSrc);

        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vs);
        _gl.AttachShader(_shaderProgram, fs);
        _gl.LinkProgram(_shaderProgram);

        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
            throw new Exception("ImGui shader link failed: " + _gl.GetProgramInfoLog(_shaderProgram));

        _gl.DetachShader(_shaderProgram, vs);
        _gl.DetachShader(_shaderProgram, fs);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
            throw new Exception($"ImGui {type} compile failed: " + _gl.GetShaderInfoLog(shader));
        return shader;
    }

    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        int fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        int fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0) return;

        _gl.GetInteger(GetPName.CurrentProgram, out int lastProgram);
        _gl.GetInteger(GetPName.TextureBinding2D, out int lastTexture);
        _gl.GetInteger(GetPName.ActiveTexture, out int lastActiveTexture);
        _gl.GetInteger(GetPName.ArrayBufferBinding, out int lastArrayBuffer);
        _gl.GetInteger(GetPName.VertexArrayBinding, out int lastVertexArray);
        bool lastEnableBlend = _gl.IsEnabled(EnableCap.Blend);
        bool lastEnableCull = _gl.IsEnabled(EnableCap.CullFace);
        bool lastEnableDepth = _gl.IsEnabled(EnableCap.DepthTest);
        bool lastEnableScissor = _gl.IsEnabled(EnableCap.ScissorTest);
        Span<int> lastViewport = stackalloc int[4];
        _gl.GetInteger(GetPName.Viewport, lastViewport);
        Span<int> lastScissor = stackalloc int[4];
        _gl.GetInteger(GetPName.ScissorBox, lastScissor);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
            BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.ScissorTest);

        _gl.Viewport(0, 0, (uint)fbWidth, (uint)fbHeight);

        float L = drawData.DisplayPos.X;
        float R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float T = drawData.DisplayPos.Y;
        float B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        Span<float> orthoProjection = stackalloc float[16]
        {
            2f/(R-L),   0,           0, 0,
            0,          2f/(T-B),    0, 0,
            0,          0,          -1, 0,
            (R+L)/(L-R),(T+B)/(B-T), 0, 1,
        };

        _gl.UseProgram(_shaderProgram);
        _gl.Uniform1(_attribLocTex, 0);
        _gl.UniformMatrix4(_attribLocProjMtx, 1, false, orthoProjection);
        _gl.BindVertexArray(_vaoHandle);

        var clipOff = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboHandle);
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()),
                (void*)cmdList.VtxBuffer.Data, BufferUsageARB.StreamDraw);

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _eboHandle);
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(cmdList.IdxBuffer.Size * sizeof(ushort)),
                (void*)cmdList.IdxBuffer.Data, BufferUsageARB.StreamDraw);

            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                var cmd = cmdList.CmdBuffer[i];
                if (cmd.UserCallback != nint.Zero) continue;

                var clipMin = new Vector2(
                    (cmd.ClipRect.X - clipOff.X) * clipScale.X,
                    (cmd.ClipRect.Y - clipOff.Y) * clipScale.Y);
                var clipMax = new Vector2(
                    (cmd.ClipRect.Z - clipOff.X) * clipScale.X,
                    (cmd.ClipRect.W - clipOff.Y) * clipScale.Y);
                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) continue;

                _gl.Scissor((int)clipMin.X, (int)(fbHeight - clipMax.Y),
                    (uint)(clipMax.X - clipMin.X), (uint)(clipMax.Y - clipMin.Y));

                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, (uint)cmd.TextureId);

                _gl.DrawElementsBaseVertex(PrimitiveType.Triangles, cmd.ElemCount,
                    DrawElementsType.UnsignedShort, (void*)(cmd.IdxOffset * sizeof(ushort)),
                    (int)cmd.VtxOffset);
            }
        }

        _gl.UseProgram((uint)lastProgram);
        _gl.BindTexture(TextureTarget.Texture2D, (uint)lastTexture);
        _gl.ActiveTexture((TextureUnit)lastActiveTexture);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)lastArrayBuffer);
        _gl.BindVertexArray((uint)lastVertexArray);
        if (lastEnableBlend) _gl.Enable(EnableCap.Blend); else _gl.Disable(EnableCap.Blend);
        if (lastEnableCull) _gl.Enable(EnableCap.CullFace); else _gl.Disable(EnableCap.CullFace);
        if (lastEnableDepth) _gl.Enable(EnableCap.DepthTest); else _gl.Disable(EnableCap.DepthTest);
        if (lastEnableScissor) _gl.Enable(EnableCap.ScissorTest); else _gl.Disable(EnableCap.ScissorTest);
        _gl.Viewport(lastViewport[0], lastViewport[1], (uint)lastViewport[2], (uint)lastViewport[3]);
        _gl.Scissor(lastScissor[0], lastScissor[1], (uint)lastScissor[2], (uint)lastScissor[3]);
    }

    private static void SetupKeyMap()
    {
        // ImGui 1.87+ uses AddKeyEvent, no key map array needed.
    }

    private static ImGuiKey SdlScancodeToImGuiKey(Scancode sc) => sc switch
    {
        Scancode.ScancodeTab => ImGuiKey.Tab,
        Scancode.ScancodeLeft => ImGuiKey.LeftArrow,
        Scancode.ScancodeRight => ImGuiKey.RightArrow,
        Scancode.ScancodeUp => ImGuiKey.UpArrow,
        Scancode.ScancodeDown => ImGuiKey.DownArrow,
        Scancode.ScancodePageup => ImGuiKey.PageUp,
        Scancode.ScancodePagedown => ImGuiKey.PageDown,
        Scancode.ScancodeHome => ImGuiKey.Home,
        Scancode.ScancodeEnd => ImGuiKey.End,
        Scancode.ScancodeInsert => ImGuiKey.Insert,
        Scancode.ScancodeDelete => ImGuiKey.Delete,
        Scancode.ScancodeBackspace => ImGuiKey.Backspace,
        Scancode.ScancodeSpace => ImGuiKey.Space,
        Scancode.ScancodeReturn => ImGuiKey.Enter,
        Scancode.ScancodeEscape => ImGuiKey.Escape,
        Scancode.ScancodeA => ImGuiKey.A,
        Scancode.ScancodeC => ImGuiKey.C,
        Scancode.ScancodeV => ImGuiKey.V,
        Scancode.ScancodeX => ImGuiKey.X,
        Scancode.ScancodeY => ImGuiKey.Y,
        Scancode.ScancodeZ => ImGuiKey.Z,
        _ => ImGuiKey.None
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ImGui.DestroyContext();

        if (_fontTexture != 0) _gl.DeleteTexture(_fontTexture);
        if (_shaderProgram != 0) _gl.DeleteProgram(_shaderProgram);
        if (_vboHandle != 0) _gl.DeleteBuffer(_vboHandle);
        if (_eboHandle != 0) _gl.DeleteBuffer(_eboHandle);
        if (_vaoHandle != 0) _gl.DeleteVertexArray(_vaoHandle);
    }
}
