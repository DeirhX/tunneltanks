// ============================================================================
// Common/MathNoise.hlsli — Hash and noise functions
// ============================================================================
//
// Procedural noise primitives used by material overlays (stone, dirt, energy).
// All functions are deterministic and seed-free — varying input coordinates
// produces different patterns.
//
//   hash21    — 2D → 1D hash (cheap, non-cryptographic)
//   valueNoise — smooth interpolated 2D noise [0..1]
//   fbmNoise   — fractal Brownian motion (summed octaves of valueNoise)
// ============================================================================

// 2D → 1D pseudo-random hash. Returns [0..1].
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Smooth 2D value noise with Hermite interpolation. Returns [0..1].
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);   // smoothstep curve
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Fractal Brownian motion: `octaves` layers of valueNoise at increasing
// frequency (×2.17 per octave) and decreasing amplitude (×0.5).
float fbmNoise(float2 p, int octaves)
{
    float val = 0.0;
    float amp = 0.5;
    for (int i = 0; i < octaves; i++)
    {
        val += amp * valueNoise(p);
        p *= 2.17;
        amp *= 0.5;
    }
    return val;
}
