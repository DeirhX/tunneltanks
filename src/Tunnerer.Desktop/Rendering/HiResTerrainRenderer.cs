namespace Tunnerer.Desktop.Rendering;

using Tunnerer.Core.Terrain;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Rendering.Textures;

public enum HiResRenderQuality
{
    Low = 0,
    Medium = 1,
    High = 2,
}

public sealed class HiResTerrainRenderer
{
    private const uint BackgroundColor = 0xFF161414;

    // Bloom
    private const int BloomBrightThreshold = 330;
    private const int BloomDownscale = 4;
    private const int BloomRadius = 3;
    private const float BloomStrength = 0.45f;

    // Vignette
    private const float VignetteDarken = 0.18f;

    // SDF edge
    private const float EdgeHalfLow = 0.20f;
    private const float EdgeHalfHigh = 0.35f;
    private const float IsolatedSolidAlpha = 0.35f;

    // Lighting
    private const float GaussianSigma = 1.8f;
    private const float AmbientWeight = 0.22f;
    private const float DiffuseWeight = 0.68f;
    private const float MacroNormalStrength = 1.8f;

    // Edge effects
    private const float OutlineThicknessLow = 0.10f;
    private const float OutlineThicknessHigh = 0.14f;
    private const float OutlineDarken = 0.45f;
    private const float SolidEdgeRange = 0.60f;
    private const float SolidEdgeDarken = 0.35f;
    private const float CaveShadowRange = 0.65f;
    private const float CaveShadowDarken = 0.50f;
    private const float AoDarken = 0.45f;

    // Rim lighting
    private const float RimDistMax = 0.30f;
    private const float RimDistMin = 0.02f;
    private const float RimStrength = 0.12f;

    // Depth darkening
    private const float DepthDarkenStart = -0.25f;
    private const float DepthDarkenFloor = 0.45f;

    // Emissive
    private const float EmissivePulseFreq = 3.0f;
    private const float EmissivePulseMin = 0.8f;
    private const float EmissivePulseRange = 0.2f;

    // Heat glow
    private const float HeatGlowR = 220f;
    private const float HeatGlowG = 80f;
    private const float HeatGlowB = 15f;
    private const float HeatGlowThreshold = 0.01f;

    // Material boundary
    private const float MaterialBlendAlphaMin = 0.1f;
    private const float MaterialBlendStrength = 0.7f;

    // Scorch
    private const float ScorchHeatFactor = 0.6f;
    private const float ScorchResidualMin = 0.2f;

    private float[]? _blurField;
    private int _blurW;
    private int _blurH;

    private readonly TerrainTextureAtlas _atlas = new();

    // Directional light (top-left, slightly forward) — normalized at static init
    private const float LightDirX = -0.35f;
    private const float LightDirY = -0.55f;
    private const float LightDirZ = 0.70f;
    private static readonly float s_lightX, s_lightY, s_lightZ;
    // Pre-computed half-vector (light + view(0,0,1)), normalized
    private static readonly float s_halfX, s_halfY, s_halfZ;

    static HiResTerrainRenderer()
    {
        float len = MathF.Sqrt(LightDirX * LightDirX + LightDirY * LightDirY + LightDirZ * LightDirZ);
        s_lightX = LightDirX / len;
        s_lightY = LightDirY / len;
        s_lightZ = LightDirZ / len;

        float hx = s_lightX, hy = s_lightY, hz = s_lightZ + 1f;
        float hLen = MathF.Sqrt(hx * hx + hy * hy + hz * hz);
        s_halfX = hx / hLen;
        s_halfY = hy / hLen;
        s_halfZ = hz / hLen;

        Gauss5x5 = new float[25];
        const float sigma = GaussianSigma;
        for (int ky = -2; ky <= 2; ky++)
            for (int kx = -2; kx <= 2; kx++)
                Gauss5x5[(ky + 2) * 5 + (kx + 2)] =
                    MathF.Exp(-(kx * kx + ky * ky) / (2f * sigma * sigma));
    }

    private static readonly float[] Gauss5x5;

    private const float TexTileDensity = 0.08f;

    // ------------------------------------------------------------------
    //  Blur-field management
    // ------------------------------------------------------------------

    public void RebuildBlurField(TerrainGrid terrain)
    {
        int w = terrain.Width, h = terrain.Height;
        if (_blurField == null || _blurW != w || _blurH != h)
        {
            _blurField = new float[w * h];
            _blurW = w;
            _blurH = h;
        }

        for (int cy = 0; cy < h; cy++)
            for (int cx = 0; cx < w; cx++)
                _blurField[cy * w + cx] = ComputeBlurCell(terrain, cx, cy, w, h);
    }

    public void UpdateBlurField(TerrainGrid terrain, IReadOnlyList<Position> dirtyCells)
    {
        if (_blurField == null) { RebuildBlurField(terrain); return; }

        int w = _blurW, h = _blurH;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            var p = dirtyCells[i];
            for (int dy = -2; dy <= 2; dy++)
            {
                int ny = p.Y + dy;
                if ((uint)ny >= (uint)h) continue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = p.X + dx;
                    if ((uint)nx >= (uint)w) continue;
                    _blurField[ny * w + nx] = ComputeBlurCell(terrain, nx, ny, w, h);
                }
            }
        }
    }

    private static float ComputeBlurCell(TerrainGrid terrain, int cx, int cy, int w, int h)
    {
        float sum = 0f, wSum = 0f;
        for (int ky = -2; ky <= 2; ky++)
        {
            int ny = cy + ky;
            if ((uint)ny >= (uint)h) continue;
            int rowOff = ny * w;
            for (int kx = -2; kx <= 2; kx++)
            {
                int nx = cx + kx;
                if ((uint)nx >= (uint)w) continue;
                float gw = Gauss5x5[(ky + 2) * 5 + (kx + 2)];
                sum += gw * (IsSolidTerrain(terrain.GetPixelRaw(rowOff + nx)) ? 1f : -1f);
                wSum += gw;
            }
        }
        return sum / wSum;
    }

    private float SampleBlur(int x, int y)
    {
        if ((uint)x >= (uint)_blurW || (uint)y >= (uint)_blurH) return 1f;
        return _blurField![y * _blurW + x];
    }

    private static float SampleHeat(TerrainGrid terrain, int x, int y, int w, int h)
    {
        if ((uint)x >= (uint)w || (uint)y >= (uint)h) return 0f;
        return terrain.GetHeat(x + y * w) / 255f;
    }

    // ------------------------------------------------------------------
    //  Public render entry points
    // ------------------------------------------------------------------

    public void Render(
        TerrainGrid terrain, uint[] targetPixels, int targetWidth, int targetHeight,
        HiResRenderQuality quality, int camPixelX, int camPixelY, int pixelScale,
        float time = 0f)
    {
        RebuildBlurField(terrain);
        RenderRegion(terrain, targetPixels, targetWidth, targetHeight, quality,
            camPixelX, camPixelY, pixelScale, 0, 0, targetWidth - 1, targetHeight - 1, time);
    }

    public void RenderStrip(
        TerrainGrid terrain, uint[] targetPixels, int targetWidth, int targetHeight,
        HiResRenderQuality quality, int camPixelX, int camPixelY, int pixelScale,
        int minX, int minY, int maxX, int maxY, float time = 0f)
    {
        RenderRegion(terrain, targetPixels, targetWidth, targetHeight, quality,
            camPixelX, camPixelY, pixelScale, minX, minY, maxX, maxY, time);
    }

    public void RenderDirty(
        TerrainGrid terrain, uint[] targetPixels, int targetWidth, int targetHeight,
        HiResRenderQuality quality, int camPixelX, int camPixelY, int pixelScale,
        IReadOnlyList<Position> dirtyCells, float time = 0f)
    {
        if (dirtyCells.Count == 0) return;
        UpdateBlurField(terrain, dirtyCells);

        const int pad = 3;
        for (int i = 0; i < dirtyCells.Count; i++)
        {
            var p = dirtyCells[i];
            int screenMinX = Clamp((p.X - pad) * pixelScale - camPixelX, 0, targetWidth - 1);
            int screenMaxX = Clamp((p.X + pad + 1) * pixelScale - 1 - camPixelX, 0, targetWidth - 1);
            int screenMinY = Clamp((p.Y - pad) * pixelScale - camPixelY, 0, targetHeight - 1);
            int screenMaxY = Clamp((p.Y + pad + 1) * pixelScale - 1 - camPixelY, 0, targetHeight - 1);

            if (screenMinX > screenMaxX || screenMinY > screenMaxY) continue;

            RenderRegion(terrain, targetPixels, targetWidth, targetHeight, quality,
                camPixelX, camPixelY, pixelScale, screenMinX, screenMinY, screenMaxX, screenMaxY, time);
        }
    }

    // ------------------------------------------------------------------
    //  Post-processing: bloom + vignette (call after entity compositing)
    // ------------------------------------------------------------------

    private static int[] _bloomBuf = Array.Empty<int>();

    public static void PostProcess(uint[] pixels, int width, int height, HiResRenderQuality quality)
    {
        if (quality >= HiResRenderQuality.Medium)
            ApplyBloom(pixels, width, height);
        if (quality >= HiResRenderQuality.High)
            ApplyVignette(pixels, width, height);
    }

    private static void ApplyBloom(uint[] pixels, int w, int h)
    {
        const int brightThreshold = BloomBrightThreshold;
        const int downscale = BloomDownscale;
        const int radius = BloomRadius;
        const float bloomStr = BloomStrength;
        int dw = w / downscale, dh = h / downscale;
        int dLen = dw * dh;

        int bloomBufSize = dLen * 6;
        if (_bloomBuf.Length < bloomBufSize)
            _bloomBuf = new int[bloomBufSize];
        var buf = _bloomBuf;
        int oR = 0, oG = dLen, oB = dLen * 2;
        int tR = dLen * 3, tG = dLen * 4, tB = dLen * 5;
        Array.Clear(buf, 0, dLen * 6);

        int brightCount = 0;
        for (int dy = 0; dy < dh; dy++)
        {
            int srcY = dy * downscale;
            int dRow = dy * dw;
            for (int dx = 0; dx < dw; dx++)
            {
                uint c = pixels[srcY * w + dx * downscale];
                int sr = (int)((c >> 16) & 0xFF);
                int sg = (int)((c >> 8) & 0xFF);
                int sb = (int)(c & 0xFF);
                if (sr + sg + sb >= brightThreshold)
                {
                    buf[oR + dRow + dx] = sr;
                    buf[oG + dRow + dx] = sg;
                    buf[oB + dRow + dx] = sb;
                    brightCount++;
                }
            }
        }
        if (brightCount < 2) return;

        // Horizontal blur
        for (int dy = 0; dy < dh; dy++)
        {
            int row = dy * dw;
            for (int dx = 0; dx < dw; dx++)
            {
                int rr = 0, gg = 0, bb = 0, cnt = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int nx = dx + k;
                    if ((uint)nx >= (uint)dw) continue;
                    int idx = row + nx;
                    if (buf[oR + idx] == 0 && buf[oG + idx] == 0 && buf[oB + idx] == 0) continue;
                    rr += buf[oR + idx]; gg += buf[oG + idx]; bb += buf[oB + idx]; cnt++;
                }
                if (cnt > 0) { buf[tR + row + dx] = rr / cnt; buf[tG + row + dx] = gg / cnt; buf[tB + row + dx] = bb / cnt; }
            }
        }

        // Vertical blur + additive blend (parallelized)
        Parallel.For(0, dh, dy2 =>
        {
            int srcY = dy2 * downscale;
            for (int dx2 = 0; dx2 < dw; dx2++)
            {
                int rr = 0, gg = 0, bb = 0, cnt = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int ny = dy2 + k;
                    if ((uint)ny >= (uint)dh) continue;
                    int idx = ny * dw + dx2;
                    if (buf[tR + idx] == 0 && buf[tG + idx] == 0 && buf[tB + idx] == 0) continue;
                    rr += buf[tR + idx]; gg += buf[tG + idx]; bb += buf[tB + idx]; cnt++;
                }
                if (cnt == 0) continue;

                int addR = (int)(rr / cnt * bloomStr);
                int addG = (int)(gg / cnt * bloomStr);
                int addB = (int)(bb / cnt * bloomStr);

                int srcX = dx2 * downscale;
                int endY = Math.Min(srcY + downscale, h);
                int endX = Math.Min(srcX + downscale, w);
                for (int py = srcY; py < endY; py++)
                {
                    int rowOff = py * w;
                    for (int px = srcX; px < endX; px++)
                    {
                        pixels[rowOff + px] = RenderingPixels.Additive(pixels[rowOff + px], addR, addG, addB);
                    }
                }
            }
        });
    }

    private static float[] _vignetteRow = Array.Empty<float>();

    private static void ApplyVignette(uint[] pixels, int w, int h)
    {
        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float invMaxDist2 = 1f / (cx * cx + cy * cy);

        if (_vignetteRow.Length < w) _vignetteRow = new float[w];
        for (int x = 0; x < w; x++)
        {
            float dx = x - cx;
            _vignetteRow[x] = dx * dx;
        }

        var dxSq = _vignetteRow;
        Parallel.For(0, h, y =>
        {
            float dyy = (y - cy) * (y - cy);
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                float d2 = (dxSq[x] + dyy) * invMaxDist2;
                float darken = 1f - d2 * VignetteDarken;

                pixels[row + x] = RenderingPixels.Darken(pixels[row + x], darken);
            }
        });
    }

    // ------------------------------------------------------------------
    //  Core render loop: textured SDF + full lighting pipeline
    // ------------------------------------------------------------------

    private void RenderRegion(
        TerrainGrid terrain, uint[] targetPixels,
        int targetWidth, int targetHeight,
        HiResRenderQuality quality,
        int camPixelX, int camPixelY, int pixelScale,
        int minX, int minY, int maxX, int maxY, float time)
    {
        int w = terrain.Width;
        int h = terrain.Height;
        float invScale = 1f / pixelScale;
        bool useTextures = quality != HiResRenderQuality.Low;
        bool useNormals = quality == HiResRenderQuality.High;

        int rowCount = maxY - minY + 1;
        bool useParallel = useNormals && rowCount > 32;

        if (useParallel)
        {
            Parallel.For(minY, maxY + 1, y =>
                RenderRow(terrain, targetPixels, targetWidth, w, h, invScale,
                    useTextures, useNormals, camPixelX, camPixelY, quality, time,
                    minX, maxX, y));
        }
        else
        {
            for (int y = minY; y <= maxY; y++)
                RenderRow(terrain, targetPixels, targetWidth, w, h, invScale,
                    useTextures, useNormals, camPixelX, camPixelY, quality, time,
                    minX, maxX, y);
        }
    }

    private void RenderRow(
        TerrainGrid terrain, uint[] targetPixels, int targetWidth,
        int w, int h, float invScale,
        bool useTextures, bool useNormals,
        int camPixelX, int camPixelY, HiResRenderQuality quality, float time,
        int minX, int maxX, int y)
    {
        float worldYf = (camPixelY + y + 0.5f) * invScale;
        int worldY = (int)worldYf;

        if (worldY < -1 || worldY > h)
        {
            int row = y * targetWidth;
            for (int x = minX; x <= maxX; x++)
                targetPixels[row + x] = BackgroundColor;
            return;
        }

        float fracY = worldYf - worldY;
        int writeIndex = minX + y * targetWidth;
        int prevCellX = int.MinValue, prevCellY = int.MinValue;
        float aoValue = 0f;

        for (int x = minX; x <= maxX; x++, writeIndex++)
            {
                float worldXf = (camPixelX + x + 0.5f) * invScale;
                int worldX = (int)worldXf;

                if (worldX < -1 || worldX > w)
                {
                    targetPixels[writeIndex] = BackgroundColor;
                    continue;
                }

                float fracX = worldXf - worldX;

                // Bilinear SDF interpolation from Gaussian blur field
                float sx = fracX - 0.5f;
                float sy = fracY - 0.5f;
                int ox = sx >= 0f ? 0 : -1;
                int oy = sy >= 0f ? 0 : -1;
                float lx = sx >= 0f ? sx : sx + 1f;
                float ly = sy >= 0f ? sy : sy + 1f;

                int cx0 = worldX + ox, cx1 = cx0 + 1;
                int cy0 = worldY + oy, cy1 = cy0 + 1;

                float b00 = SampleBlur(cx0, cy0);
                float b10 = SampleBlur(cx1, cy0);
                float b01 = SampleBlur(cx0, cy1);
                float b11 = SampleBlur(cx1, cy1);
                float dist = b00 * (1f - lx) * (1f - ly) + b10 * lx * (1f - ly) +
                             b01 * (1f - lx) * ly + b11 * lx * ly;

                // Raw terrain for material classification
                var p00 = SafeGet(terrain, cx0, cy0, w, h);
                var p10 = SafeGet(terrain, cx1, cy0, w, h);
                var p01 = SafeGet(terrain, cx0, cy1, w, h);
                var p11 = SafeGet(terrain, cx1, cy1, w, h);
                bool s00 = IsSolidTerrain(p00);
                bool s10 = IsSolidTerrain(p10);
                bool s01 = IsSolidTerrain(p01);
                bool s11 = IsSolidTerrain(p11);

                var centerPixel = SafeGet(terrain, worldX, worldY, w, h);
                var centerMatClass = TerrainTextureAtlas.Classify(centerPixel);
                var centerMatTex = _atlas.Get(centerMatClass);

                // AO (cached per cell)
                if (worldX != prevCellX || worldY != prevCellY)
                {
                    prevCellX = worldX;
                    prevCellY = worldY;
                    aoValue = ComputeAO(terrain, worldX, worldY, w, h);
                }

                // SDF edge blend
                float edgeHalf = quality == HiResRenderQuality.Low ? EdgeHalfLow : EdgeHalfHigh;
                float alpha;
                if (dist > edgeHalf) alpha = 1f;
                else if (dist < -edgeHalf) alpha = 0f;
                else alpha = Smoothstep(-edgeHalf, edgeHalf, dist);

                // Texture UV
                float texU = worldXf * TexTileDensity;
                float texV = worldYf * TexTileDensity;

                // Bilinear heat interpolation (reuses SDF sample coordinates)
                float ht00 = SampleHeat(terrain, cx0, cy0, w, h);
                float ht10 = SampleHeat(terrain, cx1, cy0, w, h);
                float ht01 = SampleHeat(terrain, cx0, cy1, w, h);
                float ht11 = SampleHeat(terrain, cx1, cy1, w, h);
                float heat = ht00 * (1f - lx) * (1f - ly) + ht10 * lx * (1f - ly) +
                             ht01 * (1f - lx) * ly + ht11 * lx * ly;
                bool isScorched = Pixel.IsScorched(centerPixel);

                // --- Color blending ---
                Color blended;
                MaterialTexture activeMat = centerMatTex;

                // Isolated solid pixels inside cave (SDF says empty but pixel is solid)
                bool centerIsSolid = IsSolidTerrain(centerPixel);
                if (alpha <= 0f && centerIsSolid)
                    alpha = IsolatedSolidAlpha;

                if (alpha >= 1f)
                {
                    blended = SampleMaterial(centerPixel, texU, texV, worldX, worldY, useTextures);
                }
                else if (alpha <= 0f)
                {
                    blended = SampleCave(texU, texV, worldX, worldY, useTextures, heat, isScorched);
                    activeMat = _atlas.Get(MaterialClass.Cave);
                }
                else
                {
                    Color solidCol;
                    MaterialTexture solidMat;
                    if (centerIsSolid)
                    {
                        solidCol = SampleMaterial(centerPixel, texU, texV, worldX, worldY, useTextures);
                        solidMat = centerMatTex;
                    }
                    else
                    {
                        var solidPixel = s00 ? p00 : s10 ? p10 : s01 ? p01 : s11 ? p11 : TerrainPixel.DirtHigh;
                        solidCol = SampleMaterial(solidPixel, texU, texV, worldX, worldY, useTextures);
                        solidMat = _atlas.Get(TerrainTextureAtlas.Classify(solidPixel));
                    }
                    Color caveCol = SampleCave(texU, texV, worldX, worldY, useTextures, heat, isScorched);
                    blended = LerpColor(caveCol, solidCol, alpha);

                    activeMat = alpha >= 0.5f ? solidMat : _atlas.Get(MaterialClass.Cave);
                }

                // --- Material-to-material boundary blending ---
                if (useTextures && alpha > MaterialBlendAlphaMin)
                {
                    var centerMat = centerMatClass;
                    var mc00 = s00 ? TerrainTextureAtlas.Classify(p00) : centerMat;
                    var mc10 = s10 ? TerrainTextureAtlas.Classify(p10) : centerMat;
                    var mc01 = s01 ? TerrainTextureAtlas.Classify(p01) : centerMat;
                    var mc11 = s11 ? TerrainTextureAtlas.Classify(p11) : centerMat;

                    bool hasBoundary = mc00 != centerMat || mc10 != centerMat ||
                                       mc01 != centerMat || mc11 != centerMat;
                    if (hasBoundary)
                    {
                        float w00 = (1f - lx) * (1f - ly);
                        float w10 = lx * (1f - ly);
                        float w01 = (1f - lx) * ly;
                        float w11 = lx * ly;

                        Color c00 = _atlas.Get(mc00).SampleColor(texU, texV);
                        Color c10 = _atlas.Get(mc10).SampleColor(texU, texV);
                        Color c01 = _atlas.Get(mc01).SampleColor(texU, texV);
                        Color c11 = _atlas.Get(mc11).SampleColor(texU, texV);

                        int blR = (int)(c00.R * w00 + c10.R * w10 + c01.R * w01 + c11.R * w11 + 0.5f);
                        int blG = (int)(c00.G * w00 + c10.G * w10 + c01.G * w01 + c11.G * w11 + 0.5f);
                        int blB = (int)(c00.B * w00 + c10.B * w10 + c01.B * w01 + c11.B * w11 + 0.5f);
                        Color bilinearCol = new((byte)blR, (byte)blG, (byte)blB);

                        float proximity = MathF.Max(
                            MathF.Abs(fracX - 0.5f),
                            MathF.Abs(fracY - 0.5f)) * 2f;
                        float blendT = Smoothstep(0f, 1f, proximity) * MaterialBlendStrength;
                        blended = LerpColor(blended, bilinearCol, blendT);
                    }
                }

                // -------------------------------------------------------
                //  Lighting pipeline
                // -------------------------------------------------------
                float rF = blended.R;
                float gF = blended.G;
                float bF = blended.B;

                if (useNormals)
                {
                    // Normal: blend texture normal + SDF macro curvature
                    var (tnx, tny, tnz) = activeMat.SampleNormal(texU, texV);

                    // At solid/cave boundary, blend normals (Phase 3: normal blending)
                    if (alpha > 0f && alpha < 1f)
                    {
                        var caveMat = _atlas.Get(MaterialClass.Cave);
                        var (cnx, cny, cnz) = caveMat.SampleNormal(texU, texV);
                        tnx = tnx * alpha + cnx * (1f - alpha);
                        tny = tny * alpha + cny * (1f - alpha);
                        tnz = tnz * alpha + cnz * (1f - alpha);
                    }

                    float dBlurDx = (SampleBlur(worldX + 1, worldY) - SampleBlur(worldX - 1, worldY)) * 0.5f;
                    float dBlurDy = (SampleBlur(worldX, worldY + 1) - SampleBlur(worldX, worldY - 1)) * 0.5f;
                    float macroStr = MacroNormalStrength;
                    float nx = tnx - dBlurDx * macroStr;
                    float ny = tny - dBlurDy * macroStr;
                    float nz = tnz;
                    float nLen = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (nLen > 0.001f) { nx /= nLen; ny /= nLen; nz /= nLen; }

                    // Diffuse (Half-Lambert)
                    float ndotl = nx * s_lightX + ny * s_lightY + nz * s_lightZ;
                    float diffuse = MathF.Max(0f, ndotl) * 0.6f + 0.4f;

                    // Specular (Blinn-Phong)
                    float ndoth = nx * s_halfX + ny * s_halfY + nz * s_halfZ;
                    ndoth = MathF.Max(0f, ndoth);
                    float specular = MathF.Pow(ndoth, activeMat.Shininess) * activeMat.SpecularIntensity;

                    // Per-material ambient color tint
                    float ambR = activeMat.AmbientTint.R / 128f;
                    float ambG = activeMat.AmbientTint.G / 128f;
                    float ambB = activeMat.AmbientTint.B / 128f;

                    float litR = rF * (AmbientWeight * ambR + DiffuseWeight * diffuse) + 255f * specular;
                    float litG = gF * (AmbientWeight * ambG + DiffuseWeight * diffuse) + 255f * specular;
                    float litB = bF * (AmbientWeight * ambB + DiffuseWeight * diffuse) + 255f * specular;

                    rF = litR;
                    gF = litG;
                    bF = litB;
                }
                else if (quality == HiResRenderQuality.Medium)
                {
                    float brightness = 1f + CellVariation(worldX, worldY) + LocalRelief(worldXf, worldYf);
                    rF *= brightness;
                    gF *= brightness;
                    bF *= brightness;
                }

                // Solid-side edge proximity darkening: dirt gradually darkens
                // approaching cave boundaries (dist 0.0..0.6 = near edge)
                if (dist > 0f && dist < SolidEdgeRange)
                {
                    float proximity = 1f - dist / SolidEdgeRange;
                    float edgeDarken = 1f - proximity * proximity * SolidEdgeDarken;
                    rF *= edgeDarken; gF *= edgeDarken; bF *= edgeDarken;
                }

                // Wall outline (narrow band right at the boundary)
                float absDist = MathF.Abs(dist);
                float outlineThick = quality == HiResRenderQuality.Low ? OutlineThicknessLow : OutlineThicknessHigh;
                if (absDist < outlineThick)
                {
                    float t = 1f - absDist / outlineThick;
                    float darken = 1f - t * t * OutlineDarken;
                    rF *= darken; gF *= darken; bF *= darken;
                }

                // Cave-side shadow gradient (wider, smoother falloff)
                if (dist < 0f && dist > -CaveShadowRange)
                {
                    float t = 1f + dist / CaveShadowRange;
                    float darken = 1f - t * t * CaveShadowDarken;
                    rF *= darken; gF *= darken; bF *= darken;
                }

                // AO on empty cells
                if (!IsSolidTerrain(centerPixel))
                {
                    float aoDarken = 1f - aoValue * AoDarken;
                    rF *= aoDarken; gF *= aoDarken; bF *= aoDarken;
                }

                // Rim lighting at terrain edges
                if (useNormals && absDist < RimDistMax && absDist > RimDistMin)
                {
                    float rimT = 1f - absDist / RimDistMax;
                    float rim = rimT * rimT * rimT * RimStrength;
                    rF += 255f * rim;
                    gF += 255f * rim;
                    bF += 255f * rim;
                }

                // Depth darkening for deep tunnels (complete fade to black)
                if (dist < DepthDarkenStart)
                {
                    float depthFactor = Remap(dist, -1f, DepthDarkenStart, DepthDarkenFloor, 1f);
                    rF *= depthFactor; gF *= depthFactor; bF *= depthFactor;
                }

                // Emissive (energy glow, scorched embers)
                if (activeMat.EmissiveIntensity > 0f)
                {
                    uint cellHash = Hash2((uint)worldX, (uint)worldY);
                    float phase = (cellHash & 0xFFu) / 255f * 6.28f;
                    float pulse = EmissivePulseMin + EmissivePulseRange * MathF.Sin(time * EmissivePulseFreq + phase);
                    float emStr = activeMat.EmissiveIntensity * pulse;
                    rF += activeMat.EmissiveColor.R * emStr;
                    gF += activeMat.EmissiveColor.G * emStr;
                    bF += activeMat.EmissiveColor.B * emStr;
                }

                // Heat glow: smooth emission from bilinearly interpolated heat
                if (heat > HeatGlowThreshold)
                {
                    float t2 = heat * heat;
                    rF += HeatGlowR * t2;
                    gF += HeatGlowG * t2 * heat;
                    bF += HeatGlowB * t2 * t2;
                }

                targetPixels[writeIndex] = RenderingPixels.PackRgb(rF, gF, bF);
            }
    }

    // ------------------------------------------------------------------
    //  Material sampling
    // ------------------------------------------------------------------

    private Color SampleMaterial(
        TerrainPixel pixel, float texU, float texV,
        int worldX, int worldY, bool useTextures)
    {
        if (!useTextures)
            return FallbackMaterialColor(pixel, worldX, worldY);

        var matClass = TerrainTextureAtlas.Classify(pixel);
        var matTex = _atlas.Get(matClass);
        Color texColor = matTex.SampleColor(texU, texV);

        uint h = Hash2((uint)worldX, (uint)worldY);
        float cellVar = ((h & 0xFFu) / 255f - 0.5f) * 0.10f;
        float brightness = 1f + cellVar;

        if (pixel == TerrainPixel.DirtGrow)
            texColor = LerpColor(texColor, new Color(94, 126, 74), 0.22f);

        return new Color(
            ScaleByte(texColor.R, brightness),
            ScaleByte(texColor.G, brightness),
            ScaleByte(texColor.B, brightness));
    }

    private Color SampleCave(float texU, float texV, int worldX, int worldY,
        bool useTextures, float heat = 0f, bool isScorched = false)
    {
        float scorchBlend = heat * ScorchHeatFactor;
        if (isScorched)
            scorchBlend = MathF.Max(scorchBlend, ScorchResidualMin);

        if (!useTextures)
        {
            uint ch = Hash2((uint)worldX, (uint)worldY);
            float t = (ch & 255u) / 255f;
            Color baseCave = LerpColor(new Color(14, 14, 16), new Color(24, 22, 26), t * 0.4f);
            if (scorchBlend > 0.01f)
            {
                Color scorch = LerpColor(new Color(22, 18, 14), new Color(38, 30, 22), t * 0.5f);
                return LerpColor(baseCave, scorch, scorchBlend);
            }
            return baseCave;
        }

        Color cave = _atlas.Get(MaterialClass.Cave).SampleColor(texU, texV);
        if (scorchBlend > 0.01f)
        {
            Color scorch = _atlas.Get(MaterialClass.Scorched).SampleColor(texU, texV);
            return LerpColor(cave, scorch, scorchBlend);
        }
        return cave;
    }

    private static Color FallbackMaterialColor(TerrainPixel pixel, int worldX, int worldY)
    {
        uint h = Hash2((uint)worldX, (uint)worldY);
        float t = (h & 1023u) / 1023f;

        if (pixel == TerrainPixel.Blank)
            return LerpColor(new Color(14, 14, 16), new Color(24, 22, 26), t * 0.3f);
        if (Pixel.IsScorched(pixel))
            return LerpColor(new Color(32, 30, 30), new Color(50, 42, 38), t * 0.35f);
        if (Pixel.IsDirt(pixel) || pixel == TerrainPixel.DirtGrow)
        {
            Color dirt = LerpColor(new Color(178, 114, 56), new Color(148, 88, 38), t * 0.65f);
            if (pixel == TerrainPixel.DirtGrow) dirt = LerpColor(dirt, new Color(94, 126, 74), 0.22f);
            return dirt;
        }
        if (Pixel.IsConcrete(pixel))
            return LerpColor(new Color(124, 124, 132), new Color(92, 92, 104), t * 0.7f);
        if (Pixel.IsRock(pixel))
            return LerpColor(new Color(94, 88, 82), new Color(72, 68, 64), t * 0.75f);
        if (Pixel.IsBase(pixel))
            return LerpColor(new Color(62, 62, 68), new Color(48, 48, 54), t * 0.25f);
        if (Pixel.IsEnergy(pixel))
            return LerpColor(new Color(150, 168, 48), new Color(240, 240, 96), t * 0.5f);
        return Pixel.GetColor(pixel);
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static TerrainPixel SafeGet(TerrainGrid terrain, int x, int y, int w, int h)
    {
        if ((uint)x >= (uint)w || (uint)y >= (uint)h) return TerrainPixel.Rock;
        return terrain.GetPixelRaw(x + y * w);
    }

    private static float Smoothstep(float lo, float hi, float x)
    {
        return RenderingMath.Smoothstep(lo, hi, x);
    }

    private static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        float t = (value - fromMin) / (fromMax - fromMin);
        t = MathF.Max(0f, MathF.Min(1f, t));
        return toMin + t * (toMax - toMin);
    }

    private static float ComputeAO(TerrainGrid terrain, int cx, int cy, int w, int h)
    {
        int solidCount = 0, sampleCount = 0;
        for (int dy = -2; dy <= 2; dy++)
        {
            int ny = cy + dy;
            if ((uint)ny >= (uint)h) continue;
            int rowOff = ny * w;
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = cx + dx;
                if ((uint)nx >= (uint)w) continue;
                sampleCount++;
                if (IsSolidTerrain(terrain.GetPixelRaw(rowOff + nx)))
                    solidCount++;
            }
        }
        return sampleCount > 0 ? (float)solidCount / sampleCount : 0f;
    }

    private static float CellVariation(int x, int y)
    {
        uint h = Hash2((uint)x, (uint)y);
        return ((h & 0xFFFFu) / 65535f - 0.5f) * 0.14f;
    }

    private static float LocalRelief(float worldXf, float worldYf)
    {
        int nx = (int)(worldXf * 2.0f);
        int ny = (int)(worldYf * 2.0f);
        return ((Hash2((uint)nx, (uint)ny) & 1023u) / 1023f - 0.5f) * 0.014f;
    }

    private static uint ApplyBrightness(Color color, float brightness)
    {
        brightness = MathF.Max(0.2f, MathF.Min(1.8f, brightness));
        return RenderingPixels.PackRgb(color.R * brightness, color.G * brightness, color.B * brightness);
    }

    private static byte ScaleByte(byte value, float scale)
    {
        int v = (int)(value * scale + 0.5f);
        return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
    }

    private static uint Hash2(uint x, uint y)
    {
        return RenderingMath.Hash2(x, y);
    }

    private static bool IsSolidTerrain(TerrainPixel p)
    {
        if (p == TerrainPixel.Blank) return false;
        if (Pixel.IsScorched(p)) return false;
        return true;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        return value > max ? max : value;
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = MathF.Max(0f, MathF.Min(1f, t));
        return new Color(LerpByte(a.R, b.R, t), LerpByte(a.G, b.G, t), LerpByte(a.B, b.B, t));
    }

    private static byte LerpByte(byte a, byte b, float t)
    {
        int v = (int)(a + (b - a) * t + 0.5f);
        return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
    }
}
