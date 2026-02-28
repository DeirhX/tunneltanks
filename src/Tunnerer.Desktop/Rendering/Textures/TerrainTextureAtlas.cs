namespace Tunnerer.Desktop.Rendering.Textures;

using Tunnerer.Core.Types;
using static ProceduralNoise;

/// <summary>
/// Material classification for texture lookup. Maps multiple TerrainPixel values
/// to a single texture set (color + normal).
/// </summary>
public enum MaterialClass : byte
{
    Cave = 0,
    Dirt,
    Rock,
    Concrete,
    Energy,
    Scorched,
    BaseWall,
    Count
}

/// <summary>
/// Holds a color texture and a normal map, both tileable and same dimensions.
/// Normal map stores (nx, ny, nz) mapped to (R, G, B) in [0..255] range
/// where 128,128,255 = flat surface pointing up.
/// </summary>
public sealed class MaterialTexture
{
    public readonly Color[] ColorMap;
    public readonly Color[] NormalMap;
    public readonly int Size;

    public float Shininess = 8f;
    public float SpecularIntensity = 0.1f;
    public Color AmbientTint = new(128, 128, 128);

    public MaterialTexture(int size)
    {
        Size = size;
        ColorMap = new Color[size * size];
        NormalMap = new Color[size * size];
    }

    /// <summary>Bilinear sample from the color map using float UV coordinates (wrapping).</summary>
    public Color SampleColor(float u, float v)
    {
        float fu = u - MathF.Floor(u);
        float fv = v - MathF.Floor(v);
        float px = fu * Size - 0.5f;
        float py = fv * Size - 0.5f;

        int x0 = ((int)MathF.Floor(px) % Size + Size) % Size;
        int y0 = ((int)MathF.Floor(py) % Size + Size) % Size;
        int x1 = (x0 + 1) % Size;
        int y1 = (y0 + 1) % Size;

        float fx = px - MathF.Floor(px);
        float fy = py - MathF.Floor(py);

        var c00 = ColorMap[y0 * Size + x0];
        var c10 = ColorMap[y0 * Size + x1];
        var c01 = ColorMap[y1 * Size + x0];
        var c11 = ColorMap[y1 * Size + x1];

        return Bilerp(c00, c10, c01, c11, fx, fy);
    }

    /// <summary>Bilinear sample from the normal map using float UV coordinates (wrapping).</summary>
    public (float nx, float ny, float nz) SampleNormal(float u, float v)
    {
        float fu = u - MathF.Floor(u);
        float fv = v - MathF.Floor(v);
        float px = fu * Size - 0.5f;
        float py = fv * Size - 0.5f;

        int x0 = ((int)MathF.Floor(px) % Size + Size) % Size;
        int y0 = ((int)MathF.Floor(py) % Size + Size) % Size;
        int x1 = (x0 + 1) % Size;
        int y1 = (y0 + 1) % Size;

        float fx = px - MathF.Floor(px);
        float fy = py - MathF.Floor(py);

        var n00 = DecodeNormal(NormalMap[y0 * Size + x0]);
        var n10 = DecodeNormal(NormalMap[y0 * Size + x1]);
        var n01 = DecodeNormal(NormalMap[y1 * Size + x0]);
        var n11 = DecodeNormal(NormalMap[y1 * Size + x1]);

        float rnx = Bilerp(n00.nx, n10.nx, n01.nx, n11.nx, fx, fy);
        float rny = Bilerp(n00.ny, n10.ny, n01.ny, n11.ny, fx, fy);
        float rnz = Bilerp(n00.nz, n10.nz, n01.nz, n11.nz, fx, fy);
        float len = MathF.Sqrt(rnx * rnx + rny * rny + rnz * rnz);
        if (len > 0.001f) { rnx /= len; rny /= len; rnz /= len; }
        else { rnx = 0; rny = 0; rnz = 1f; }
        return (rnx, rny, rnz);
    }

    private static (float nx, float ny, float nz) DecodeNormal(Color c)
    {
        float nx = c.R / 127.5f - 1f;
        float ny = c.G / 127.5f - 1f;
        float nz = c.B / 127.5f - 1f;
        return (nx, ny, nz);
    }

    private static Color EncodeNormal(float nx, float ny, float nz)
    {
        return new Color(
            (byte)((nx * 0.5f + 0.5f) * 255f + 0.5f),
            (byte)((ny * 0.5f + 0.5f) * 255f + 0.5f),
            (byte)((nz * 0.5f + 0.5f) * 255f + 0.5f));
    }

    private static Color Bilerp(Color c00, Color c10, Color c01, Color c11, float fx, float fy)
    {
        float r = Bilerp(c00.R, c10.R, c01.R, c11.R, fx, fy);
        float g = Bilerp(c00.G, c10.G, c01.G, c11.G, fx, fy);
        float b = Bilerp(c00.B, c10.B, c01.B, c11.B, fx, fy);
        return new Color(Clamp(r), Clamp(g), Clamp(b));
    }

    private static float Bilerp(float a00, float a10, float a01, float a11, float fx, float fy)
    {
        float top = a00 + fx * (a10 - a00);
        float bot = a01 + fx * (a11 - a01);
        return top + fy * (bot - top);
    }

    private static byte Clamp(float v) => (byte)MathF.Max(0f, MathF.Min(255f, v + 0.5f));

    // ----------------------------------------------------------------
    //  Normal map generation from height map
    // ----------------------------------------------------------------

    /// <summary>
    /// Generate a normal map from a height field. Heights are 0..1 floats, same size as texture.
    /// <paramref name="strength"/> controls how pronounced the bumps are.
    /// </summary>
    public void GenerateNormalsFromHeights(float[] heights, float strength = 1.5f)
    {
        int s = Size;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float hL = heights[y * s + ((x - 1 + s) % s)];
            float hR = heights[y * s + ((x + 1) % s)];
            float hU = heights[((y - 1 + s) % s) * s + x];
            float hD = heights[((y + 1) % s) * s + x];

            float dx = (hR - hL) * strength;
            float dy = (hD - hU) * strength;
            float nx = -dx;
            float ny = -dy;
            float nz = 1f;
            float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= len; ny /= len; nz /= len;

            NormalMap[y * s + x] = EncodeNormal(nx, ny, nz);
        }
    }
}

/// <summary>
/// Atlas of all terrain material textures, generated procedurally at startup.
/// Access textures by <see cref="MaterialClass"/>.
/// </summary>
public sealed class TerrainTextureAtlas
{
    public const int TexSize = 64;

    private readonly MaterialTexture[] _textures;

    public TerrainTextureAtlas()
    {
        _textures = new MaterialTexture[(int)MaterialClass.Count];
        GenerateAll();
    }

    public MaterialTexture Get(MaterialClass mat) => _textures[(int)mat];

    /// <summary>
    /// Classify a TerrainPixel into its MaterialClass for texture lookup.
    /// </summary>
    public static MaterialClass Classify(Core.Terrain.TerrainPixel pixel)
    {
        if (pixel == Core.Terrain.TerrainPixel.Blank)
            return MaterialClass.Cave;
        if (Core.Terrain.Pixel.IsScorched(pixel))
            return MaterialClass.Scorched;
        if (Core.Terrain.Pixel.IsDirt(pixel) || pixel == Core.Terrain.TerrainPixel.DirtGrow)
            return MaterialClass.Dirt;
        if (Core.Terrain.Pixel.IsRock(pixel))
            return MaterialClass.Rock;
        if (Core.Terrain.Pixel.IsConcrete(pixel))
            return MaterialClass.Concrete;
        if (Core.Terrain.Pixel.IsEnergy(pixel))
            return MaterialClass.Energy;
        if (Core.Terrain.Pixel.IsBase(pixel))
            return MaterialClass.BaseWall;
        return MaterialClass.Rock; // fallback
    }

    // ================================================================
    //  Procedural texture generators
    // ================================================================

    private void GenerateAll()
    {
        _textures[(int)MaterialClass.Cave] = GenerateCave();
        _textures[(int)MaterialClass.Dirt] = GenerateDirt();
        _textures[(int)MaterialClass.Rock] = GenerateRock();
        _textures[(int)MaterialClass.Concrete] = GenerateConcrete();
        _textures[(int)MaterialClass.Energy] = GenerateEnergy();
        _textures[(int)MaterialClass.Scorched] = GenerateScorched();
        _textures[(int)MaterialClass.BaseWall] = GenerateBaseWall();
    }

    // ----------------------------------------------------------------
    //  Dirt: organic soil with grain, subtle vein-like structures
    // ----------------------------------------------------------------

    private static MaterialTexture GenerateDirt()
    {
        var tex = new MaterialTexture(TexSize);
        int s = TexSize;
        var heights = new float[s * s];

        Color warmA = new(178, 114, 56);
        Color warmB = new(148, 88, 38);
        Color darkAccent = new(120, 72, 30);
        Color rootTint = new(100, 80, 42);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = (float)x / s;
            float v = (float)y / s;

            // Base fbm for soil grain
            float grain = Fbm(u, v, 4, 5, 2f, 0.55f, 100);

            // Worley for clumpy organic structure
            float cell = Worley(u * 6, v * 6, 6, 200);
            float organic = Remap(cell, 0.05f, 0.5f, 0f, 1f);

            // Root-like veins (stretched Worley)
            float vein = Worley(u * 3, v * 8, 3, 300);
            float veinMask = 1f - Remap(vein, 0f, 0.12f, 0f, 0.4f);

            float t = grain * 0.6f + organic * 0.3f;
            Color baseCol = LerpColor(warmA, warmB, t);
            baseCol = LerpColor(baseCol, darkAccent, (1f - organic) * 0.25f);
            baseCol = LerpColor(baseCol, rootTint, (1f - veinMask) * 0.3f);

            heights[y * s + x] = grain * 0.7f + organic * 0.3f;
            tex.ColorMap[y * s + x] = baseCol;
        }

        tex.GenerateNormalsFromHeights(heights, 2.0f);
        tex.Shininess = 4f;
        tex.SpecularIntensity = 0.05f;
        tex.AmbientTint = new Color(140, 120, 100);
        return tex;
    }

    // ----------------------------------------------------------------
    //  Rock: stratified stone with cracks and mineral flecks
    // ----------------------------------------------------------------

    private static MaterialTexture GenerateRock()
    {
        var tex = new MaterialTexture(TexSize);
        int s = TexSize;
        var heights = new float[s * s];

        Color stoneA = new(94, 88, 82);
        Color stoneB = new(74, 70, 66);
        Color crackCol = new(50, 48, 46);
        Color mineralFleck = new(130, 120, 108);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = (float)x / s;
            float v = (float)y / s;

            // Base stone noise
            float base_ = Fbm(u, v, 4, 4, 2f, 0.5f, 400);

            // Horizontal strata (stretched noise)
            float strata = Fbm(u * 0.5f, v * 4f, 2, 3, 2f, 0.6f, 500);

            // Cracks via sharp Worley edges
            float crack = Worley(u * 5, v * 5, 5, 600);
            float crackMask = 1f - Remap(crack, 0f, 0.06f, 0f, 1f);

            // Mineral flecks
            float fleck = Fbm(u, v, 8, 2, 2f, 0.3f, 700);
            float fleckMask = fleck > 0.72f ? (fleck - 0.72f) / 0.28f : 0f;

            float t = base_ * 0.5f + strata * 0.4f;
            Color col = LerpColor(stoneA, stoneB, t);
            col = LerpColor(col, crackCol, crackMask * 0.5f);
            col = LerpColor(col, mineralFleck, fleckMask * 0.4f);

            heights[y * s + x] = base_ * 0.5f + strata * 0.3f - crackMask * 0.3f;
            tex.ColorMap[y * s + x] = col;
        }

        tex.GenerateNormalsFromHeights(heights, 2.5f);
        tex.Shininess = 8f;
        tex.SpecularIntensity = 0.12f;
        tex.AmbientTint = new Color(110, 115, 125);
        return tex;
    }

    // ----------------------------------------------------------------
    //  Concrete: poured aggregate with joint lines
    // ----------------------------------------------------------------

    private static MaterialTexture GenerateConcrete()
    {
        var tex = new MaterialTexture(TexSize);
        int s = TexSize;
        var heights = new float[s * s];

        Color concreteA = new(128, 128, 136);
        Color concreteB = new(100, 100, 112);
        Color aggregateCol = new(140, 140, 148);
        Color jointCol = new(72, 72, 80);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = (float)x / s;
            float v = (float)y / s;

            // Smooth concrete base
            float base_ = Fbm(u, v, 4, 3, 2f, 0.4f, 800);

            // Aggregate specks (high frequency)
            float agg = Fbm(u, v, 8, 2, 2f, 0.35f, 900);
            float aggMask = agg > 0.6f ? (agg - 0.6f) / 0.4f : 0f;

            // Joint lines (brick-like pattern)
            float jointX = MathF.Abs((u * 3f) % 1f - 0.5f);
            float jointY = MathF.Abs((v * 2f) % 1f - 0.5f);
            float joint = MathF.Min(jointX, jointY);
            float jointMask = joint < 0.04f ? 1f - joint / 0.04f : 0f;

            float t = base_;
            Color col = LerpColor(concreteA, concreteB, t * 0.7f);
            col = LerpColor(col, aggregateCol, aggMask * 0.3f);
            col = LerpColor(col, jointCol, jointMask * 0.45f);

            heights[y * s + x] = base_ * 0.3f + aggMask * 0.2f - jointMask * 0.5f;
            tex.ColorMap[y * s + x] = col;
        }

        tex.GenerateNormalsFromHeights(heights, 1.8f);
        tex.Shininess = 6f;
        tex.SpecularIntensity = 0.08f;
        tex.AmbientTint = new Color(120, 120, 130);
        return tex;
    }

    // ----------------------------------------------------------------
    //  Energy: crystalline ore with internal facets
    // ----------------------------------------------------------------

    private static MaterialTexture GenerateEnergy()
    {
        var tex = new MaterialTexture(TexSize);
        int s = TexSize;
        var heights = new float[s * s];

        Color crystalDark = new(120, 140, 36);
        Color crystalBright = new(220, 230, 80);
        Color facetHighlight = new(255, 255, 160);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = (float)x / s;
            float v = (float)y / s;

            // Faceted crystal structure via Worley
            float w1 = Worley(u * 5, v * 5, 5, 1000);
            float w2 = Worley(u * 8, v * 8, 8, 1100);
            float facet = Remap(w1, 0f, 0.4f, 0f, 1f);
            float detail = Remap(w2, 0f, 0.2f, 0f, 1f);

            // Internal glow variation
            float glow = Fbm(u, v, 4, 3, 2f, 0.5f, 1200);

            float t = facet * 0.6f + glow * 0.4f;
            Color col = LerpColor(crystalDark, crystalBright, t);

            // Bright facet edge highlights
            float edgeDist = MathF.Abs(w1 - 0.15f);
            if (edgeDist < 0.03f)
                col = LerpColor(col, facetHighlight, (1f - edgeDist / 0.03f) * 0.5f);

            heights[y * s + x] = facet * 0.6f + detail * 0.3f;
            tex.ColorMap[y * s + x] = col;
        }

        tex.GenerateNormalsFromHeights(heights, 3.0f);
        tex.Shininess = 32f;
        tex.SpecularIntensity = 0.5f;
        tex.AmbientTint = new Color(100, 130, 90);
        return tex;
    }

    // ----------------------------------------------------------------
    //  Scorched: charred earth with cracks and ember traces
    // ----------------------------------------------------------------

    private static MaterialTexture GenerateScorched()
    {
        var tex = new MaterialTexture(TexSize);
        int s = TexSize;
        var heights = new float[s * s];

        Color charDark = new(55, 45, 40);
        Color charLight = new(85, 70, 55);
        Color emberCol = new(150, 65, 25);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = (float)x / s;
            float v = (float)y / s;

            float base_ = Fbm(u, v, 4, 4, 2f, 0.5f, 1300);

            // Cracks
            float crack = Worley(u * 6, v * 6, 6, 1400);
            float crackMask = 1f - Remap(crack, 0f, 0.05f, 0f, 1f);

            // Ember traces (rare hot spots)
            float ember = Fbm(u, v, 8, 2, 2f, 0.3f, 1500);
            float emberMask = ember > 0.65f ? (ember - 0.65f) / 0.35f : 0f;

            Color col = LerpColor(charDark, charLight, base_ * 0.5f);
            col = LerpColor(col, new Color(35, 30, 28), crackMask * 0.5f);
            col = LerpColor(col, emberCol, emberMask * 0.45f);

            heights[y * s + x] = base_ * 0.4f - crackMask * 0.5f;
            tex.ColorMap[y * s + x] = col;
        }

        tex.GenerateNormalsFromHeights(heights, 2.2f);
        tex.Shininess = 4f;
        tex.SpecularIntensity = 0.04f;
        tex.AmbientTint = new Color(125, 110, 100);
        return tex;
    }

    // ----------------------------------------------------------------
    //  Base wall: metal paneling with rivets
    // ----------------------------------------------------------------

    private static MaterialTexture GenerateBaseWall()
    {
        var tex = new MaterialTexture(TexSize);
        int s = TexSize;
        var heights = new float[s * s];

        Color metalA = new(62, 62, 68);
        Color metalB = new(48, 48, 54);
        Color rivetCol = new(80, 80, 88);
        Color seamCol = new(36, 36, 42);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = (float)x / s;
            float v = (float)y / s;

            // Brushed metal grain (stretched noise)
            float grain = Fbm(u * 8f, v * 1f, 8, 3, 2f, 0.4f, 1600);

            // Panel seams
            float seamX = MathF.Abs((u * 2f) % 1f - 0.5f);
            float seamY = MathF.Abs((v * 2f) % 1f - 0.5f);
            float seam = MathF.Min(seamX, seamY);
            float seamMask = seam < 0.03f ? 1f - seam / 0.03f : 0f;

            // Rivets (point features at panel corners)
            float ru = (u * 2f) % 1f;
            float rv = (v * 2f) % 1f;
            float cornerDist = MathF.Min(
                MathF.Min(ru * ru + rv * rv, (1f - ru) * (1f - ru) + rv * rv),
                MathF.Min(ru * ru + (1f - rv) * (1f - rv), (1f - ru) * (1f - ru) + (1f - rv) * (1f - rv)));
            float rivetMask = cornerDist < 0.008f ? 1f - cornerDist / 0.008f : 0f;

            Color col = LerpColor(metalA, metalB, grain);
            col = LerpColor(col, seamCol, seamMask * 0.5f);
            col = LerpColor(col, rivetCol, rivetMask * 0.5f);

            heights[y * s + x] = 0.5f + grain * 0.2f - seamMask * 0.4f + rivetMask * 0.3f;
            tex.ColorMap[y * s + x] = col;
        }

        tex.GenerateNormalsFromHeights(heights, 2.0f);
        tex.Shininess = 12f;
        tex.SpecularIntensity = 0.15f;
        tex.AmbientTint = new Color(110, 110, 120);
        return tex;
    }

    // ----------------------------------------------------------------
    //  Cave: dark stone void with subtle moisture
    // ----------------------------------------------------------------

    private static MaterialTexture GenerateCave()
    {
        var tex = new MaterialTexture(TexSize);
        int s = TexSize;
        var heights = new float[s * s];

        Color voidDark = new(14, 14, 16);
        Color voidLight = new(24, 22, 26);
        Color moistureCol = new(18, 22, 28);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = (float)x / s;
            float v = (float)y / s;

            float base_ = Fbm(u, v, 4, 3, 2f, 0.5f, 1700);
            float moisture = Fbm(u * 2f, v * 2f, 4, 2, 2f, 0.4f, 1800);

            Color col = LerpColor(voidDark, voidLight, base_ * 0.5f);
            col = LerpColor(col, moistureCol, moisture * 0.2f);

            heights[y * s + x] = base_ * 0.3f;
            tex.ColorMap[y * s + x] = col;
        }

        tex.GenerateNormalsFromHeights(heights, 1.0f);
        tex.Shininess = 16f;
        tex.SpecularIntensity = 0.25f;
        tex.AmbientTint = new Color(100, 105, 120);
        return tex;
    }
}
