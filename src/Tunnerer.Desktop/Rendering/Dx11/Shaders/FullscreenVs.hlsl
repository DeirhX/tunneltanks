// ============================================================================
// FullscreenVs.hlsl — Full-screen triangle vertex shader
// ============================================================================
//
// Generates a single oversized triangle from SV_VertexID (0, 1, 2) that
// covers the entire viewport. No vertex buffer needed — just issue Draw(3).
// UV coordinates map [0,0] at top-left to [1,1] at bottom-right.
//
// Used by both TerrainPs.hlsl and PostPs/Main.hlsl.
// ============================================================================

struct VSOut
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

VSOut main(uint id : SV_VertexID)
{
    VSOut o;
    if (id == 0)      { o.pos = float4(-1, -1, 0, 1); o.uv = float2(0, 1); }
    else if (id == 1) { o.pos = float4(-1,  3, 0, 1); o.uv = float2(0, -1); }
    else              { o.pos = float4( 3, -1, 0, 1); o.uv = float2(2, 1); }
    return o;
}
