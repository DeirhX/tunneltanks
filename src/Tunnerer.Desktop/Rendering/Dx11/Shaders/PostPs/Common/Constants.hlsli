// Shared semantic constants for post-process passes.
static const float3 kLumaWeights = float3(0.299, 0.587, 0.114);
static const float2 kSignedEpsilon2 = float2(1e-4, -1e-4);
static const float kRadiusEpsilonPx = 1e-3;

// Terrain material ID encoding in aux.b channel.
static const float kMaterialCodeScale = 255.0;
static const float kMaterialCodeDirt = 85.0;
static const float kMaterialCodeStone = 170.0;
static const float kMaterialCodeEnergy = 212.0;
static const float kMaterialCodeBase = 255.0;
static const float kMaterialMaskWidthWide = 40.0;
static const float kMaterialMaskWidthNarrow = 28.0;

// a0.r stores normalized heat channel; decode to gameplay temperature-like units.
static const float kHeatDebugTemperatureScale = 255.0 * 4.0;
