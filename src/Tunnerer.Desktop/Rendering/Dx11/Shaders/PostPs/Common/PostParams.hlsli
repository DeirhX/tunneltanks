// ============================================================================
// Common/PostParams.hlsli — Shared PostParams cbuffer layout
// ============================================================================
//
// Single source of truth for all CPU->shader post-process constants.
// Included by both PostPs and TerrainPs shaders to prevent layout drift.
// ============================================================================

static const float kPostParamsLayoutVersion = 2.0;

cbuffer PostParams : register(b0)
{
    // ---- Screen / world geometry ------------------------------------------
    float2 TexelSize;               // 1 / render-target resolution (in UV)
    float PixelScale;               // World-cell → screen-pixel scale factor
    float Time;                     // Elapsed game time (seconds), for animation
    float2 WorldSize;               // World grid dimensions (cells)
    float2 CameraPixels;            // Camera top-left corner (screen pixels)
    float2 ViewSize;                // Render-target resolution (pixels)

    // ---- Feature toggles / post toggles -----------------------------------
    float UseTerrainAux;            // > 0.5 to enable terrain SDF / material passes
    float PostParamsLayoutVersion;  // Must match kPostParamsLayoutVersion
    float HeatDebugOverlay;         // > 0.5 to show temperature color overlay
    float PostVignetteEnabled;      // > 0.5 to run vignette
    float PostTerrainCurveEnabled;  // > 0.5 to run terrain curve pass
    float PostTerrainAuxEnabled;    // > 0.5 to run terrain texture pass
    float PostTankGlowEnabled;      // > 0.5 to run tank glow pass
    float PostTerrainHeatEnabled;   // > 0.5 to run terrain heat/scorch pass

    // ---- Vignette ---------------------------------------------------------
    float VignetteStrength;         // Darkening intensity at screen edges
    float VignetteInnerRadius;      // UV distance where darkening begins
    float VignetteOuterRadius;      // UV distance where darkening reaches full

    // ---- Terrain SDF shading (TerrainAuxPass) -----------------------------
    float TerrainMaskEdgeStrength;  // Sobel edge response multiplier
    float TerrainMaskCaveDarken;    // Cave-side darkening factor
    float TerrainMaskSolidLift;     // Solid-side brightness lift
    float TerrainMaskOutlineDarken; // Outline ring darkening
    float TerrainMaskRimLift;       // Rim highlight on boundary ring
    float TerrainMaskBoundaryScale; // Width scaling for boundary ring detection

    // ---- Quality level ----------------------------------------------------
    float Quality;                  // 0 = minimal, higher = enable extra shading detail

    // ---- Material emissive ------------------------------------------------
    float4 MaterialEmissiveEnergy;  // .rgb = energy vein glow, .a = intensity
    float4 MaterialEmissiveScorched;// .rgb = scorch glow color, .a = intensity
    float4 MaterialEmissivePulse;   // .x = speed, .y = base, .z = amplitude, .w = unused

    // ---- TerrainPs native-continuous rendering ----------------------------
    float4 NativeContinuousParams;  // .x = EdgeSoftness, .y = BoundaryBlend, .w = CurvingEnabled

    // ---- Directional lighting (material shading) --------------------------
    float4 LightDir;                // .xyz = light direction, .w = NormalStrength
    float4 HalfVector;              // .xyz = half-vector (view+light), .w = MicroNormalStrength
    float4 LightParams;             // .x = Ambient, .y = DiffuseWeight, .z = Shininess, .w = SpecularIntensity

    // ---- Tank heat glow ---------------------------------------------------
    float4 TankHeatGlowColor;       // .rgb = glow tint, .a = distortion enable flag

    // ---- Terrain heat / scorch glow ---------------------------------------
    float4 TerrainHeatGlowColorAndThreshold; // .rgb = glow tint, .a = onset threshold

    // ---- Per-tank glow positions ------------------------------------------
    float TankGlowCount;            // Number of active entries in TankGlow[]
    float4 TankGlow[8];             // .xy = screen UV, .z = radius, .w = heat [0..1]
};
