namespace Tunnerer.Desktop.Rendering;

using ImGuiNET;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using Tunnerer.Core.Config;
using Tunnerer.Core.Types;
using GL_PixelFormat = Silk.NET.OpenGL.PixelFormat;
using GL_PixelType = Silk.NET.OpenGL.PixelType;

public sealed unsafe class OpenGlGameRenderBackend : IGameRenderBackend
{
    private const int MaxTankGlowCount = 8;
    private readonly GL _gl;
    private readonly ImGuiController _imgui;
    private readonly uint[] _gameTextures = new uint[2];
    private int _gameTexIndex;
    private int _gameTexW;
    private int _gameTexH;
    private uint _postSourceTexture;
    private uint _postFbo;
    private uint _postProgram;
    private uint _postVao;
    private uint _postVbo;
    private uint _terrainAuxTexture;
    private int _terrainAuxW;
    private int _terrainAuxH;
    private byte[] _terrainAuxUploadScratch = Array.Empty<byte>();
    private int _postLocScene;
    private int _postLocAuxTex;
    private int _postLocTexelSize;
    private int _postLocQuality;
    private int _postLocTankGlowCount;
    private int _postLocTankGlowData0;
    private int _postLocUseTerrainAux;
    private int _postLocWorldSize;
    private int _postLocCameraPixels;
    private int _postLocViewSize;
    private int _postLocPixelScale;
    private int _postLocTime;
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
    private int _postLocTerrainMaskEdgeStrength;
    private int _postLocTerrainMaskCaveDarken;
    private int _postLocTerrainMaskSolidLift;
    private int _postLocTerrainMaskOutlineDarken;
    private int _postLocTerrainMaskRimLift;
    private int _postLocTerrainMaskBoundaryScale;
    private int _postLocTerrainHeatThreshold;
    private int _postLocVignetteInnerRadius;
    private int _postLocVignetteOuterRadius;
    private int _postLocMaterialEmissiveEnergyColor;
    private int _postLocMaterialEmissiveScorchedColor;
    private int _postLocMaterialEmissiveEnergyStrength;
    private int _postLocMaterialEmissiveScorchedStrength;
    private int _postLocMaterialEmissivePulseFreq;
    private int _postLocMaterialEmissivePulseMin;
    private int _postLocMaterialEmissivePulseRange;
    private bool _disposed;

    public nint GameTextureId => (nint)_gameTextures[_gameTexIndex];
    public bool SupportsUi => true;

    public OpenGlGameRenderBackend(GL gl, ImGuiController imgui)
    {
        _gl = gl;
        _imgui = imgui;
    }

    public void ProcessEvent(Event ev) => _imgui.ProcessEvent(ev);

    public void NewFrame(int windowW, int windowH, float deltaTime) => _imgui.NewFrame(windowW, windowH, deltaTime);

    public void Render() => _imgui.Render();

    public void ClearFrame(Size viewportSize, Tunnerer.Core.Types.Color clearColor)
    {
        _gl.Viewport(0, 0, (uint)viewportSize.X, (uint)viewportSize.Y);
        _gl.ClearColor(
            clearColor.R / 255f,
            clearColor.G / 255f,
            clearColor.B / 255f,
            clearColor.A / 255f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void UploadGamePixels(in GamePixelsUpload upload)
    {
        uint[] pixels = upload.Pixels;
        Size viewSize = upload.View.ViewSize;
        Size worldSize = upload.View.WorldSize;
        Position cameraPixels = upload.View.CameraPixels;
        int pixelScale = upload.View.PixelScale;

        EnsureGameTextures(viewSize.X, viewSize.Y);
        EnsurePostProcessObjects(viewSize.X, viewSize.Y);
        UpdateTerrainAuxTexture(upload.TerrainAux, worldSize, upload.AuxDirtyRect);
        int uploadIdx = 1 - _gameTexIndex;
        _gl.BindTexture(TextureTarget.Texture2D, _postSourceTexture);
        fixed (uint* ptr = pixels)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                (uint)viewSize.X, (uint)viewSize.Y, GL_PixelFormat.Bgra, GL_PixelType.UnsignedByte, ptr);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        bool useTerrainAux = upload.TerrainAux != null && worldSize.X > 0 && worldSize.Y > 0 && pixelScale > 0;
        RunPostProcessPass(_postSourceTexture, _gameTextures[uploadIdx], upload.View, upload.Quality,
            upload.TankHeatGlowData, upload.TankHeatGlowCount, useTerrainAux);
        _gameTexIndex = uploadIdx;
    }

    private void EnsureGameTextures(int width, int height)
    {
        if (_gameTextures[0] != 0 && _gameTexW == width && _gameTexH == height)
            return;
        for (int i = 0; i < 2; i++)
        {
            if (_gameTextures[i] != 0) _gl.DeleteTexture(_gameTextures[i]);
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
        if (_postFbo == 0) _postFbo = _gl.GenFramebuffer();
        if (_postProgram == 0) CreatePostProcessProgram();
        if (_postVao != 0) return;
        _postVao = _gl.GenVertexArray();
        _postVbo = _gl.GenBuffer();
        _gl.BindVertexArray(_postVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _postVbo);
        float[] quad =
        {
            -1f, -1f, 0f, 0f, 1f, -1f, 1f, 0f, 1f, 1f, 1f, 1f,
            -1f, -1f, 0f, 0f, 1f, 1f, 1f, 1f, -1f, 1f, 0f, 1f,
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
uniform sampler2D uAuxTex;
uniform vec2 uTexelSize;
uniform int uQuality;
uniform int uTankGlowCount;
uniform vec4 uTankGlow[8];
uniform int uUseTerrainAux;
uniform vec2 uWorldSize;
uniform vec2 uCameraPixels;
uniform vec2 uViewSize;
uniform float uPixelScale;
uniform float uTime;
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
uniform float uTerrainMaskEdgeStrength;
uniform float uTerrainMaskCaveDarken;
uniform float uTerrainMaskSolidLift;
uniform float uTerrainMaskOutlineDarken;
uniform float uTerrainMaskRimLift;
uniform float uTerrainMaskBoundaryScale;
uniform float uTerrainHeatThreshold;
uniform float uVignetteInnerRadius;
uniform float uVignetteOuterRadius;
uniform vec3 uMaterialEmissiveEnergyColor;
uniform vec3 uMaterialEmissiveScorchedColor;
uniform float uMaterialEmissiveEnergyStrength;
uniform float uMaterialEmissiveScorchedStrength;
uniform float uMaterialEmissivePulseFreq;
uniform float uMaterialEmissivePulseMin;
uniform float uMaterialEmissivePulseRange;
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
        float vig = 1.0 - smoothstep(uVignetteInnerRadius, uVignetteOuterRadius, d) * uVignetteStrength;
        color *= vig;
    }
    if (uQuality >= 1) {
        float l = dot(texture(uScene, vUv + vec2(-uTexelSize.x, 0.0)).rgb, vec3(0.299, 0.587, 0.114));
        float r = dot(texture(uScene, vUv + vec2(uTexelSize.x, 0.0)).rgb, vec3(0.299, 0.587, 0.114));
        float u = dot(texture(uScene, vUv + vec2(0.0, -uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
        float d = dot(texture(uScene, vUv + vec2(0.0, uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
        float edge = abs(r - l) + abs(d - u);
        float edgeLift = max(0.0, edge - uEdgeLightBias) * uEdgeLightStrength;
        color += vec3(edgeLift);
    }
    if (uUseTerrainAux > 0 && uPixelScale > 0.0) {
        vec2 screenPx = vUv * uViewSize;
        vec2 worldCell = (uCameraPixels + screenPx) / uPixelScale;
        vec2 auxUv = (worldCell + vec2(0.5, 0.5)) / uWorldSize;
        vec2 mTexel = vec2(1.0 / uWorldSize.x, 1.0 / uWorldSize.y);
        vec4 a0 = texture(uAuxTex, auxUv);
        vec4 ax1 = texture(uAuxTex, auxUv + vec2(mTexel.x, 0.0));
        vec4 ax2 = texture(uAuxTex, auxUv - vec2(mTexel.x, 0.0));
        vec4 ay1 = texture(uAuxTex, auxUv + vec2(0.0, mTexel.y));
        vec4 ay2 = texture(uAuxTex, auxUv - vec2(0.0, mTexel.y));
        float m0 = a0.g;
        float mx1 = ax1.g;
        float mx2 = ax2.g;
        float my1 = ay1.g;
        float my2 = ay2.g;
        float edge = abs(mx1 - mx2) + abs(my1 - my2);
        float edgeAmt = min(1.0, edge * uTerrainMaskEdgeStrength);
        float boundary = 1.0 - abs(m0 * 2.0 - 1.0);
        float outline = min(1.0, boundary * uTerrainMaskBoundaryScale);
        color *= 1.0 - outline * uTerrainMaskOutlineDarken;
        if (m0 < 0.5) color *= 1.0 - edgeAmt * uTerrainMaskCaveDarken;
        else {
            color += vec3(edgeAmt * uTerrainMaskSolidLift);
            color += vec3(edgeAmt * outline * uTerrainMaskRimLift);
        }
        float heat = a0.r * 0.50 + (ax1.r + ax2.r + ay1.r + ay2.r) * 0.125;
        if (heat > uTerrainHeatThreshold) {
            float t2 = heat * heat;
            color.r += uTerrainHeatGlowColor.r * t2;
            color.g += uTerrainHeatGlowColor.g * t2 * heat;
            color.b += uTerrainHeatGlowColor.b * t2 * t2;
        }
        float phase = fract(sin(dot(floor(worldCell), vec2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
        float pulse = uMaterialEmissivePulseMin + uMaterialEmissivePulseRange * (0.5 + 0.5 * sin(uTime * uMaterialEmissivePulseFreq + phase));
        color += uMaterialEmissiveEnergyColor * (a0.b * uMaterialEmissiveEnergyStrength * pulse);
        color += uMaterialEmissiveScorchedColor * (a0.a * uMaterialEmissiveScorchedStrength * pulse);
    }
    for (int i = 0; i < 8; i++) {
        if (i >= uTankGlowCount) break;
        vec4 g = uTankGlow[i];
        float falloff = 1.0 - dot(vUv - g.xy, vUv - g.xy) / max(1e-6, g.z * g.z);
        if (falloff > 0.0) {
            falloff *= falloff;
            color += uTankHeatGlowColor * (g.w * falloff);
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
        if (status == 0) throw new Exception("Post-process shader link failed: " + _gl.GetProgramInfoLog(_postProgram));
        _gl.DetachShader(_postProgram, vs);
        _gl.DetachShader(_postProgram, fs);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        _postLocScene = _gl.GetUniformLocation(_postProgram, "uScene");
        _postLocAuxTex = _gl.GetUniformLocation(_postProgram, "uAuxTex");
        _postLocTexelSize = _gl.GetUniformLocation(_postProgram, "uTexelSize");
        _postLocQuality = _gl.GetUniformLocation(_postProgram, "uQuality");
        _postLocTankGlowCount = _gl.GetUniformLocation(_postProgram, "uTankGlowCount");
        _postLocTankGlowData0 = _gl.GetUniformLocation(_postProgram, "uTankGlow[0]");
        _postLocUseTerrainAux = _gl.GetUniformLocation(_postProgram, "uUseTerrainAux");
        _postLocWorldSize = _gl.GetUniformLocation(_postProgram, "uWorldSize");
        _postLocCameraPixels = _gl.GetUniformLocation(_postProgram, "uCameraPixels");
        _postLocViewSize = _gl.GetUniformLocation(_postProgram, "uViewSize");
        _postLocPixelScale = _gl.GetUniformLocation(_postProgram, "uPixelScale");
        _postLocTime = _gl.GetUniformLocation(_postProgram, "uTime");
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
        _postLocTerrainMaskEdgeStrength = _gl.GetUniformLocation(_postProgram, "uTerrainMaskEdgeStrength");
        _postLocTerrainMaskCaveDarken = _gl.GetUniformLocation(_postProgram, "uTerrainMaskCaveDarken");
        _postLocTerrainMaskSolidLift = _gl.GetUniformLocation(_postProgram, "uTerrainMaskSolidLift");
        _postLocTerrainMaskOutlineDarken = _gl.GetUniformLocation(_postProgram, "uTerrainMaskOutlineDarken");
        _postLocTerrainMaskRimLift = _gl.GetUniformLocation(_postProgram, "uTerrainMaskRimLift");
        _postLocTerrainMaskBoundaryScale = _gl.GetUniformLocation(_postProgram, "uTerrainMaskBoundaryScale");
        _postLocTerrainHeatThreshold = _gl.GetUniformLocation(_postProgram, "uTerrainHeatThreshold");
        _postLocVignetteInnerRadius = _gl.GetUniformLocation(_postProgram, "uVignetteInnerRadius");
        _postLocVignetteOuterRadius = _gl.GetUniformLocation(_postProgram, "uVignetteOuterRadius");
        _postLocMaterialEmissiveEnergyColor = _gl.GetUniformLocation(_postProgram, "uMaterialEmissiveEnergyColor");
        _postLocMaterialEmissiveScorchedColor = _gl.GetUniformLocation(_postProgram, "uMaterialEmissiveScorchedColor");
        _postLocMaterialEmissiveEnergyStrength = _gl.GetUniformLocation(_postProgram, "uMaterialEmissiveEnergyStrength");
        _postLocMaterialEmissiveScorchedStrength = _gl.GetUniformLocation(_postProgram, "uMaterialEmissiveScorchedStrength");
        _postLocMaterialEmissivePulseFreq = _gl.GetUniformLocation(_postProgram, "uMaterialEmissivePulseFreq");
        _postLocMaterialEmissivePulseMin = _gl.GetUniformLocation(_postProgram, "uMaterialEmissivePulseMin");
        _postLocMaterialEmissivePulseRange = _gl.GetUniformLocation(_postProgram, "uMaterialEmissivePulseRange");
    }

    private void RunPostProcessPass(uint sourceTex, uint destTex, in RenderView view, HiResRenderQuality quality,
        float[]? tankHeatGlowData, int tankHeatGlowCount, bool useTerrainAux)
    {
        int width = view.ViewSize.X;
        int height = view.ViewSize.Y;
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
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, destTex, 0);
        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.UseProgram(_postProgram);
        _gl.Uniform1(_postLocScene, 0);
        _gl.Uniform1(_postLocAuxTex, 1);
        _gl.Uniform2(_postLocTexelSize, 1f / width, 1f / height);
        _gl.Uniform1(_postLocQuality, (int)quality);
        _gl.Uniform1(_postLocUseTerrainAux, useTerrainAux ? 1 : 0);
        _gl.Uniform2(_postLocWorldSize, (float)view.WorldSize.X, (float)view.WorldSize.Y);
        _gl.Uniform2(_postLocCameraPixels, (float)view.CameraPixels.X, (float)view.CameraPixels.Y);
        _gl.Uniform2(_postLocViewSize, (float)view.ViewSize.X, (float)view.ViewSize.Y);
        _gl.Uniform1(_postLocPixelScale, (float)view.PixelScale);
        _gl.Uniform1(_postLocTime, (float)ImGui.GetTime());
        _gl.Uniform1(_postLocBloomThreshold, Tweaks.Screen.PostBloomThreshold);
        _gl.Uniform1(_postLocBloomStrength, Tweaks.Screen.PostBloomStrength);
        _gl.Uniform1(_postLocBloomWeightCenter, Tweaks.Screen.PostBloomWeightCenter);
        _gl.Uniform1(_postLocBloomWeightAxis, Tweaks.Screen.PostBloomWeightAxis);
        _gl.Uniform1(_postLocBloomWeightDiagonal, Tweaks.Screen.PostBloomWeightDiagonal);
        _gl.Uniform1(_postLocVignetteStrength, Tweaks.Screen.PostVignetteStrength);
        _gl.Uniform1(_postLocVignetteInnerRadius, Tweaks.Screen.PostVignetteInnerRadius);
        _gl.Uniform1(_postLocVignetteOuterRadius, Tweaks.Screen.PostVignetteOuterRadius);
        _gl.Uniform1(_postLocEdgeLightStrength, Tweaks.Screen.PostTerrainEdgeLightStrength);
        _gl.Uniform1(_postLocEdgeLightBias, Tweaks.Screen.PostTerrainEdgeLightBias);
        _gl.Uniform3(_postLocTankHeatGlowColor, Tweaks.Screen.PostTankHeatGlowR, Tweaks.Screen.PostTankHeatGlowG, Tweaks.Screen.PostTankHeatGlowB);
        _gl.Uniform3(_postLocTerrainHeatGlowColor, Tweaks.Screen.PostTerrainHeatGlowR, Tweaks.Screen.PostTerrainHeatGlowG, Tweaks.Screen.PostTerrainHeatGlowB);
        _gl.Uniform1(_postLocTerrainMaskEdgeStrength, Tweaks.Screen.PostTerrainMaskEdgeStrength);
        _gl.Uniform1(_postLocTerrainMaskCaveDarken, Tweaks.Screen.PostTerrainMaskCaveDarken);
        _gl.Uniform1(_postLocTerrainMaskSolidLift, Tweaks.Screen.PostTerrainMaskSolidLift);
        _gl.Uniform1(_postLocTerrainMaskOutlineDarken, Tweaks.Screen.PostTerrainMaskOutlineDarken);
        _gl.Uniform1(_postLocTerrainMaskRimLift, Tweaks.Screen.PostTerrainMaskRimLift);
        _gl.Uniform1(_postLocTerrainMaskBoundaryScale, Tweaks.Screen.PostTerrainMaskBoundaryScale);
        _gl.Uniform1(_postLocTerrainHeatThreshold, Tweaks.Screen.PostTerrainHeatThreshold);
        _gl.Uniform3(_postLocMaterialEmissiveEnergyColor, Tweaks.Screen.PostMaterialEmissiveEnergyR, Tweaks.Screen.PostMaterialEmissiveEnergyG, Tweaks.Screen.PostMaterialEmissiveEnergyB);
        _gl.Uniform3(_postLocMaterialEmissiveScorchedColor, Tweaks.Screen.PostMaterialEmissiveScorchedR, Tweaks.Screen.PostMaterialEmissiveScorchedG, Tweaks.Screen.PostMaterialEmissiveScorchedB);
        _gl.Uniform1(_postLocMaterialEmissiveEnergyStrength, Tweaks.Screen.PostMaterialEmissiveEnergyStrength);
        _gl.Uniform1(_postLocMaterialEmissiveScorchedStrength, Tweaks.Screen.PostMaterialEmissiveScorchedStrength);
        _gl.Uniform1(_postLocMaterialEmissivePulseFreq, Tweaks.Screen.PostMaterialEmissivePulseFreq);
        _gl.Uniform1(_postLocMaterialEmissivePulseMin, Tweaks.Screen.PostMaterialEmissivePulseMin);
        _gl.Uniform1(_postLocMaterialEmissivePulseRange, Tweaks.Screen.PostMaterialEmissivePulseRange);
        int clampedGlowCount = Math.Clamp(tankHeatGlowCount, 0, MaxTankGlowCount);
        _gl.Uniform1(_postLocTankGlowCount, clampedGlowCount);
        if (tankHeatGlowData != null)
        {
            for (int i = 0; i < clampedGlowCount; i++)
            {
                int baseIdx = i * 4;
                _gl.Uniform4(_postLocTankGlowData0 + i,
                    tankHeatGlowData[baseIdx + 0], tankHeatGlowData[baseIdx + 1],
                    tankHeatGlowData[baseIdx + 2], tankHeatGlowData[baseIdx + 3]);
            }
        }
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, sourceTex);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _terrainAuxTexture);
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

    private void UpdateTerrainAuxTexture(byte[]? auxData, Size worldSize, Rect? dirtyRect)
    {
        int worldWidth = worldSize.X;
        int worldHeight = worldSize.Y;
        if (auxData == null || worldWidth <= 0 || worldHeight <= 0) return;
        bool sizeChanged = _terrainAuxW != worldWidth || _terrainAuxH != worldHeight;
        if (_terrainAuxTexture == 0)
        {
            _terrainAuxTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _terrainAuxTexture);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            sizeChanged = true;
        }
        else
        {
            _gl.BindTexture(TextureTarget.Texture2D, _terrainAuxTexture);
        }
        if (sizeChanged)
        {
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            fixed (byte* ptr = auxData)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)worldWidth, (uint)worldHeight, 0, GL_PixelFormat.Rgba, GL_PixelType.UnsignedByte, ptr);
            }
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }
        else if (dirtyRect is Rect rect)
        {
            RectMath.GetMinMaxInclusive(rect, out int minX, out int minY, out int maxX, out int maxY);
            int rw = maxX - minX + 1;
            int rh = maxY - minY + 1;
            int rectLen = rw * rh * 4;
            if (_terrainAuxUploadScratch.Length < rectLen) _terrainAuxUploadScratch = new byte[rectLen];
            for (int y = 0; y < rh; y++)
            {
                int src = ((minY + y) * worldWidth + minX) * 4;
                int dst = y * rw * 4;
                Array.Copy(auxData, src, _terrainAuxUploadScratch, dst, rw * 4);
            }
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            fixed (byte* ptr = _terrainAuxUploadScratch)
            {
                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, minX, minY, (uint)rw, (uint)rh, GL_PixelFormat.Rgba, GL_PixelType.UnsignedByte, ptr);
            }
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _terrainAuxW = worldWidth;
        _terrainAuxH = worldHeight;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0) throw new Exception($"Post {type} compile failed: " + _gl.GetShaderInfoLog(shader));
        return shader;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _imgui.Dispose();
        for (int i = 0; i < 2; i++) if (_gameTextures[i] != 0) _gl.DeleteTexture(_gameTextures[i]);
        if (_postSourceTexture != 0) _gl.DeleteTexture(_postSourceTexture);
        if (_terrainAuxTexture != 0) _gl.DeleteTexture(_terrainAuxTexture);
        if (_postFbo != 0) _gl.DeleteFramebuffer(_postFbo);
        if (_postProgram != 0) _gl.DeleteProgram(_postProgram);
        if (_postVbo != 0) _gl.DeleteBuffer(_postVbo);
        if (_postVao != 0) _gl.DeleteVertexArray(_postVao);
    }
}
