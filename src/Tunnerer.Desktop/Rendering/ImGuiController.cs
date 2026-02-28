namespace Tunnerer.Desktop.Rendering;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.SDL;
using Tunnerer.Core.Config;
using GL_PixelFormat = Silk.NET.OpenGL.PixelFormat;
using GL_PixelType = Silk.NET.OpenGL.PixelType;
using Silk.NET.OpenGL;

/// <summary>
/// Self-contained SDL2 + OpenGL3.3 backend for Dear ImGui.
/// Handles context init, event forwarding, font atlas, and rendering.
/// Also manages the game-world texture upload.
/// </summary>
public sealed unsafe class ImGuiController : IDisposable
{
    private const int MaxTankGlowCount = 8;
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

    private readonly uint[] _gameTextures = new uint[2];
    private int _gameTexIndex;
    private int _gameTexW;
    private int _gameTexH;
    private uint _postSourceTexture;
    private uint _postFbo;
    private uint _postProgram;
    private uint _postVao;
    private uint _postVbo;
    private uint _terrainHeatTexture;
    private int _terrainHeatW;
    private int _terrainHeatH;
    private byte[] _terrainHeatUploadScratch = Array.Empty<byte>();
    private int _postLocScene;
    private int _postLocHeatTex;
    private int _postLocTexelSize;
    private int _postLocQuality;
    private int _postLocTankGlowCount;
    private int _postLocTankGlowData0;
    private int _postLocUseTerrainHeat;
    private int _postLocWorldSize;
    private int _postLocCameraPixels;
    private int _postLocViewSize;
    private int _postLocPixelScale;
    private int _postLocBloomThreshold;
    private int _postLocBloomStrength;
    private int _postLocBloomWeightCenter;
    private int _postLocBloomWeightAxis;
    private int _postLocBloomWeightDiagonal;
    private int _postLocVignetteStrength;
    private int _postLocEdgeLightStrength;
    private int _postLocEdgeLightBias;
    private int _postLocTankHeatGlowColor;
    private int _postLocTerrainHeatGlowColor;

    private ulong _perfFrequency;
    private ulong _time;
    private bool _disposed;

    public GL Gl => _gl;
    public uint GameTextureId => _gameTextures[_gameTexIndex];

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

        _perfFrequency = _sdl.GetPerformanceFrequency();
        _time = _sdl.GetPerformanceCounter();
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

    public void EnsureGameTextures(int width, int height)
    {
        if (_gameTextures[0] != 0 && _gameTexW == width && _gameTexH == height)
            return;

        for (int i = 0; i < 2; i++)
        {
            if (_gameTextures[i] != 0)
                _gl.DeleteTexture(_gameTextures[i]);

            _gameTextures[i] = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _gameTextures[i]);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, 0, GL_PixelFormat.Bgra, GL_PixelType.UnsignedByte, null);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        _gameTexW = width;
        _gameTexH = height;
    }

    public void UploadGamePixels(uint[] pixels, int width, int height)
    {
        UploadGamePixels(pixels, width, height, HiResRenderQuality.High);
    }

    public void UploadGamePixels(uint[] pixels, int width, int height, HiResRenderQuality quality)
    {
        UploadGamePixels(pixels, width, height, quality, null, 0);
    }

    public void UploadGamePixels(
        uint[] pixels, int width, int height, HiResRenderQuality quality,
        float[]? tankHeatGlowData, int tankHeatGlowCount)
    {
        UploadGamePixels(pixels, width, height, quality, tankHeatGlowData, tankHeatGlowCount,
            null, 0, 0, 0, 0, 1, false, 0, 0, 0, 0);
    }

    public void UploadGamePixels(
        uint[] pixels, int width, int height, HiResRenderQuality quality,
        float[]? tankHeatGlowData, int tankHeatGlowCount,
        byte[]? terrainHeat, int worldWidth, int worldHeight, int camPixelX, int camPixelY, int pixelScale,
        bool hasHeatDirtyRect, int heatMinX, int heatMinY, int heatMaxX, int heatMaxY)
    {
        EnsureGameTextures(width, height);
        EnsurePostProcessObjects(width, height);
        UpdateTerrainHeatTexture(terrainHeat, worldWidth, worldHeight, hasHeatDirtyRect,
            heatMinX, heatMinY, heatMaxX, heatMaxY);
        int uploadIdx = 1 - _gameTexIndex;
        _gl.BindTexture(TextureTarget.Texture2D, _postSourceTexture);
        fixed (uint* ptr = pixels)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                (uint)width, (uint)height, GL_PixelFormat.Bgra, GL_PixelType.UnsignedByte, ptr);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        // GPU offload for bloom/vignette and dynamic heat glow over the full frame.
        RunPostProcessPass(_postSourceTexture, _gameTextures[uploadIdx], width, height, quality,
            tankHeatGlowData, tankHeatGlowCount,
            terrainHeat != null && worldWidth > 0 && worldHeight > 0 && pixelScale > 0,
            worldWidth, worldHeight, camPixelX, camPixelY, pixelScale);
        _gameTexIndex = uploadIdx;
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

    private void EnsurePostProcessObjects(int width, int height)
    {
        if (_postSourceTexture == 0)
        {
            _postSourceTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _postSourceTexture);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, 0, GL_PixelFormat.Bgra, GL_PixelType.UnsignedByte, null);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
        }
        else if (_gameTexW != width || _gameTexH != height)
        {
            _gl.BindTexture(TextureTarget.Texture2D, _postSourceTexture);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, 0, GL_PixelFormat.Bgra, GL_PixelType.UnsignedByte, null);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        if (_postFbo == 0)
            _postFbo = _gl.GenFramebuffer();

        if (_postProgram == 0)
            CreatePostProcessProgram();

        if (_postVao == 0)
        {
            _postVao = _gl.GenVertexArray();
            _postVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_postVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _postVbo);

            float[] quad =
            {
                -1f, -1f, 0f, 0f,
                 1f, -1f, 1f, 0f,
                 1f,  1f, 1f, 1f,
                -1f, -1f, 0f, 0f,
                 1f,  1f, 1f, 1f,
                -1f,  1f, 0f, 1f,
            };
            fixed (float* ptr = quad)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
        }
    }

    private void CreatePostProcessProgram()
    {
        const string vertSrc = @"#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aUv;
out vec2 vUv;
void main() {
    vUv = aUv;
    gl_Position = vec4(aPos, 0.0, 1.0);
}";

        const string fragSrc = @"#version 330 core
in vec2 vUv;
uniform sampler2D uScene;
uniform sampler2D uHeatTex;
uniform vec2 uTexelSize;
uniform int uQuality;
uniform int uTankGlowCount;
uniform vec4 uTankGlow[8]; // x=u, y=v, z=radiusUv, w=intensity
uniform int uUseTerrainHeat;
uniform vec2 uWorldSize;
uniform vec2 uCameraPixels;
uniform vec2 uViewSize;
uniform float uPixelScale;
uniform float uBloomThreshold;
uniform float uBloomStrength;
uniform float uBloomWeightCenter;
uniform float uBloomWeightAxis;
uniform float uBloomWeightDiagonal;
uniform float uVignetteStrength;
uniform float uEdgeLightStrength;
uniform float uEdgeLightBias;
uniform vec3 uTankHeatGlowColor;
uniform vec3 uTerrainHeatGlowColor;
layout (location = 0) out vec4 Out_Color;

vec3 bright(vec3 c) { return max(c - vec3(uBloomThreshold), vec3(0.0)); }

void main() {
    vec3 base = texture(uScene, vUv).rgb;
    vec3 color = base;

    if (uQuality >= 1) {
        vec2 tx = vec2(uTexelSize.x, 0.0);
        vec2 ty = vec2(0.0, uTexelSize.y);
        vec2 d1 = vec2(uTexelSize.x, uTexelSize.y);
        vec2 d2 = vec2(uTexelSize.x, -uTexelSize.y);
        vec3 bloom = bright(base) * uBloomWeightCenter;
        bloom += bright(texture(uScene, vUv + tx).rgb) * uBloomWeightAxis;
        bloom += bright(texture(uScene, vUv - tx).rgb) * uBloomWeightAxis;
        bloom += bright(texture(uScene, vUv + ty).rgb) * uBloomWeightAxis;
        bloom += bright(texture(uScene, vUv - ty).rgb) * uBloomWeightAxis;
        bloom += bright(texture(uScene, vUv + d1).rgb) * uBloomWeightDiagonal;
        bloom += bright(texture(uScene, vUv - d1).rgb) * uBloomWeightDiagonal;
        bloom += bright(texture(uScene, vUv + d2).rgb) * uBloomWeightDiagonal;
        bloom += bright(texture(uScene, vUv - d2).rgb) * uBloomWeightDiagonal;
        color += bloom * uBloomStrength;
    }

    if (uQuality >= 2) {
        float d = distance(vUv, vec2(0.5, 0.5));
        float vig = 1.0 - smoothstep(0.35, 0.95, d) * uVignetteStrength;
        color *= vig;
    }

    // First terrain-lighting GPU slice: cheap screen-space edge lift.
    if (uQuality >= 1) {
        float l = dot(texture(uScene, vUv + vec2(-uTexelSize.x, 0.0)).rgb, vec3(0.299, 0.587, 0.114));
        float r = dot(texture(uScene, vUv + vec2(uTexelSize.x, 0.0)).rgb, vec3(0.299, 0.587, 0.114));
        float u = dot(texture(uScene, vUv + vec2(0.0, -uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
        float d = dot(texture(uScene, vUv + vec2(0.0, uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
        float edge = abs(r - l) + abs(d - u);
        float edgeLift = max(0.0, edge - uEdgeLightBias) * uEdgeLightStrength;
        color += vec3(edgeLift);
    }

    for (int i = 0; i < 8; i++) {
        if (i >= uTankGlowCount) break;
        vec4 g = uTankGlow[i];
        float falloff = 1.0 - dot(vUv - g.xy, vUv - g.xy) / max(1e-6, g.z * g.z);
        if (falloff > 0.0) {
            falloff *= falloff;
            float glow = g.w * falloff;
            color += uTankHeatGlowColor * glow;
        }
    }

    if (uUseTerrainHeat > 0 && uPixelScale > 0.0) {
        vec2 screenPx = vUv * uViewSize;
        vec2 worldCell = (uCameraPixels + screenPx) / uPixelScale;
        vec2 heatUv = (worldCell + vec2(0.5, 0.5)) / uWorldSize;
        float h0 = texture(uHeatTex, heatUv).r;
        vec2 hTexel = vec2(1.0 / uWorldSize.x, 1.0 / uWorldSize.y);
        float h1 = texture(uHeatTex, heatUv + vec2(hTexel.x, 0.0)).r;
        float h2 = texture(uHeatTex, heatUv - vec2(hTexel.x, 0.0)).r;
        float h3 = texture(uHeatTex, heatUv + vec2(0.0, hTexel.y)).r;
        float h4 = texture(uHeatTex, heatUv - vec2(0.0, hTexel.y)).r;
        float heat = h0 * 0.50 + (h1 + h2 + h3 + h4) * 0.125;
        if (heat > 0.01) {
            float t2 = heat * heat;
            color.r += uTerrainHeatGlowColor.r * t2;
            color.g += uTerrainHeatGlowColor.g * t2 * heat;
            color.b += uTerrainHeatGlowColor.b * t2 * t2;
        }
    }

    Out_Color = vec4(color, 1.0);
}";

        uint vs = CompileShader(ShaderType.VertexShader, vertSrc);
        uint fs = CompileShader(ShaderType.FragmentShader, fragSrc);
        _postProgram = _gl.CreateProgram();
        _gl.AttachShader(_postProgram, vs);
        _gl.AttachShader(_postProgram, fs);
        _gl.LinkProgram(_postProgram);
        _gl.GetProgram(_postProgram, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
            throw new Exception("Post-process shader link failed: " + _gl.GetProgramInfoLog(_postProgram));
        _gl.DetachShader(_postProgram, vs);
        _gl.DetachShader(_postProgram, fs);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);

        _postLocScene = _gl.GetUniformLocation(_postProgram, "uScene");
        _postLocHeatTex = _gl.GetUniformLocation(_postProgram, "uHeatTex");
        _postLocTexelSize = _gl.GetUniformLocation(_postProgram, "uTexelSize");
        _postLocQuality = _gl.GetUniformLocation(_postProgram, "uQuality");
        _postLocTankGlowCount = _gl.GetUniformLocation(_postProgram, "uTankGlowCount");
        _postLocTankGlowData0 = _gl.GetUniformLocation(_postProgram, "uTankGlow[0]");
        _postLocUseTerrainHeat = _gl.GetUniformLocation(_postProgram, "uUseTerrainHeat");
        _postLocWorldSize = _gl.GetUniformLocation(_postProgram, "uWorldSize");
        _postLocCameraPixels = _gl.GetUniformLocation(_postProgram, "uCameraPixels");
        _postLocViewSize = _gl.GetUniformLocation(_postProgram, "uViewSize");
        _postLocPixelScale = _gl.GetUniformLocation(_postProgram, "uPixelScale");
        _postLocBloomThreshold = _gl.GetUniformLocation(_postProgram, "uBloomThreshold");
        _postLocBloomStrength = _gl.GetUniformLocation(_postProgram, "uBloomStrength");
        _postLocBloomWeightCenter = _gl.GetUniformLocation(_postProgram, "uBloomWeightCenter");
        _postLocBloomWeightAxis = _gl.GetUniformLocation(_postProgram, "uBloomWeightAxis");
        _postLocBloomWeightDiagonal = _gl.GetUniformLocation(_postProgram, "uBloomWeightDiagonal");
        _postLocVignetteStrength = _gl.GetUniformLocation(_postProgram, "uVignetteStrength");
        _postLocEdgeLightStrength = _gl.GetUniformLocation(_postProgram, "uEdgeLightStrength");
        _postLocEdgeLightBias = _gl.GetUniformLocation(_postProgram, "uEdgeLightBias");
        _postLocTankHeatGlowColor = _gl.GetUniformLocation(_postProgram, "uTankHeatGlowColor");
        _postLocTerrainHeatGlowColor = _gl.GetUniformLocation(_postProgram, "uTerrainHeatGlowColor");
    }

    private void RunPostProcessPass(
        uint sourceTex, uint destTex, int width, int height, HiResRenderQuality quality,
        float[]? tankHeatGlowData, int tankHeatGlowCount,
        bool useTerrainHeat, int worldWidth, int worldHeight, int camPixelX, int camPixelY, int pixelScale)
    {
        _gl.GetInteger(GetPName.CurrentProgram, out int lastProgram);
        _gl.GetInteger(GetPName.ActiveTexture, out int lastActiveTexture);
        _gl.GetInteger(GetPName.TextureBinding2D, out int lastTexture);
        _gl.GetInteger(GetPName.VertexArrayBinding, out int lastVao);
        _gl.GetInteger(GetPName.ArrayBufferBinding, out int lastArrayBuffer);
        Span<int> lastViewport = stackalloc int[4];
        _gl.GetInteger(GetPName.Viewport, lastViewport);
        bool lastBlend = _gl.IsEnabled(EnableCap.Blend);
        bool lastDepth = _gl.IsEnabled(EnableCap.DepthTest);
        bool lastCull = _gl.IsEnabled(EnableCap.CullFace);
        bool lastScissor = _gl.IsEnabled(EnableCap.ScissorTest);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, destTex, 0);

        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.ScissorTest);

        _gl.UseProgram(_postProgram);
        _gl.Uniform1(_postLocScene, 0);
        _gl.Uniform1(_postLocHeatTex, 1);
        _gl.Uniform2(_postLocTexelSize, 1f / width, 1f / height);
        _gl.Uniform1(_postLocQuality, (int)quality);
        _gl.Uniform1(_postLocUseTerrainHeat, useTerrainHeat ? 1 : 0);
        _gl.Uniform2(_postLocWorldSize, (float)worldWidth, (float)worldHeight);
        _gl.Uniform2(_postLocCameraPixels, (float)camPixelX, (float)camPixelY);
        _gl.Uniform2(_postLocViewSize, (float)width, (float)height);
        _gl.Uniform1(_postLocPixelScale, (float)pixelScale);
        _gl.Uniform1(_postLocBloomThreshold, Tweaks.Screen.PostBloomThreshold);
        _gl.Uniform1(_postLocBloomStrength, Tweaks.Screen.PostBloomStrength);
        _gl.Uniform1(_postLocBloomWeightCenter, Tweaks.Screen.PostBloomWeightCenter);
        _gl.Uniform1(_postLocBloomWeightAxis, Tweaks.Screen.PostBloomWeightAxis);
        _gl.Uniform1(_postLocBloomWeightDiagonal, Tweaks.Screen.PostBloomWeightDiagonal);
        _gl.Uniform1(_postLocVignetteStrength, Tweaks.Screen.PostVignetteStrength);
        _gl.Uniform1(_postLocEdgeLightStrength, Tweaks.Screen.PostTerrainEdgeLightStrength);
        _gl.Uniform1(_postLocEdgeLightBias, Tweaks.Screen.PostTerrainEdgeLightBias);
        _gl.Uniform3(_postLocTankHeatGlowColor,
            Tweaks.Screen.PostTankHeatGlowR, Tweaks.Screen.PostTankHeatGlowG, Tweaks.Screen.PostTankHeatGlowB);
        _gl.Uniform3(_postLocTerrainHeatGlowColor,
            Tweaks.Screen.PostTerrainHeatGlowR, Tweaks.Screen.PostTerrainHeatGlowG, Tweaks.Screen.PostTerrainHeatGlowB);
        int clampedGlowCount = Math.Clamp(tankHeatGlowCount, 0, MaxTankGlowCount);
        _gl.Uniform1(_postLocTankGlowCount, clampedGlowCount);
        if (tankHeatGlowData != null)
        {
            for (int i = 0; i < clampedGlowCount; i++)
            {
                int baseIdx = i * 4;
                _gl.Uniform4(_postLocTankGlowData0 + i,
                    tankHeatGlowData[baseIdx + 0],
                    tankHeatGlowData[baseIdx + 1],
                    tankHeatGlowData[baseIdx + 2],
                    tankHeatGlowData[baseIdx + 3]);
            }
        }
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, sourceTex);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _terrainHeatTexture);
        _gl.BindVertexArray(_postVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        _gl.BindVertexArray((uint)lastVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)lastArrayBuffer);
        _gl.BindTexture(TextureTarget.Texture2D, (uint)lastTexture);
        _gl.ActiveTexture((TextureUnit)lastActiveTexture);
        _gl.UseProgram((uint)lastProgram);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(lastViewport[0], lastViewport[1], (uint)lastViewport[2], (uint)lastViewport[3]);
        if (lastBlend) _gl.Enable(EnableCap.Blend); else _gl.Disable(EnableCap.Blend);
        if (lastDepth) _gl.Enable(EnableCap.DepthTest); else _gl.Disable(EnableCap.DepthTest);
        if (lastCull) _gl.Enable(EnableCap.CullFace); else _gl.Disable(EnableCap.CullFace);
        if (lastScissor) _gl.Enable(EnableCap.ScissorTest); else _gl.Disable(EnableCap.ScissorTest);
    }

    private void UpdateTerrainHeatTexture(
        byte[]? heatData, int worldWidth, int worldHeight, bool hasDirtyRect,
        int minX, int minY, int maxX, int maxY)
    {
        if (heatData == null || worldWidth <= 0 || worldHeight <= 0)
            return;

        bool sizeChanged = _terrainHeatW != worldWidth || _terrainHeatH != worldHeight;
        if (_terrainHeatTexture == 0)
        {
            _terrainHeatTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _terrainHeatTexture);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            sizeChanged = true;
        }
        else
        {
            _gl.BindTexture(TextureTarget.Texture2D, _terrainHeatTexture);
        }

        if (sizeChanged)
        {
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            fixed (byte* ptr = heatData)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8,
                    (uint)worldWidth, (uint)worldHeight, 0,
                    GL_PixelFormat.Red, GL_PixelType.UnsignedByte, ptr);
            }
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }
        else if (hasDirtyRect)
        {
            int rw = maxX - minX + 1;
            int rh = maxY - minY + 1;
            int rectLen = rw * rh;
            if (_terrainHeatUploadScratch.Length < rectLen)
                _terrainHeatUploadScratch = new byte[rectLen];

            for (int y = 0; y < rh; y++)
            {
                int src = (minY + y) * worldWidth + minX;
                int dst = y * rw;
                Array.Copy(heatData, src, _terrainHeatUploadScratch, dst, rw);
            }

            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            fixed (byte* ptr = _terrainHeatUploadScratch)
            {
                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, minX, minY,
                    (uint)rw, (uint)rh, GL_PixelFormat.Red, GL_PixelType.UnsignedByte, ptr);
            }
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _terrainHeatW = worldWidth;
        _terrainHeatH = worldHeight;
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
        for (int i = 0; i < 2; i++)
            if (_gameTextures[i] != 0) _gl.DeleteTexture(_gameTextures[i]);
        if (_postSourceTexture != 0) _gl.DeleteTexture(_postSourceTexture);
        if (_terrainHeatTexture != 0) _gl.DeleteTexture(_terrainHeatTexture);
        if (_postFbo != 0) _gl.DeleteFramebuffer(_postFbo);
        if (_postProgram != 0) _gl.DeleteProgram(_postProgram);
        if (_postVbo != 0) _gl.DeleteBuffer(_postVbo);
        if (_postVao != 0) _gl.DeleteVertexArray(_postVao);
        if (_shaderProgram != 0) _gl.DeleteProgram(_shaderProgram);
        if (_vboHandle != 0) _gl.DeleteBuffer(_vboHandle);
        if (_eboHandle != 0) _gl.DeleteBuffer(_eboHandle);
        if (_vaoHandle != 0) _gl.DeleteVertexArray(_vaoHandle);
    }
}
