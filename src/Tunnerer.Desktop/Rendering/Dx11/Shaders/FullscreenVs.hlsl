struct VSOut
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

VSOut main(uint id : SV_VertexID)
{
    VSOut o;
    if (id == 0) { o.pos = float4(-1, -1, 0, 1); o.uv = float2(0, 1); }
    else if (id == 1) { o.pos = float4(-1, 3, 0, 1); o.uv = float2(0, -1); }
    else { o.pos = float4(3, -1, 0, 1); o.uv = float2(2, 1); }
    return o;
}
