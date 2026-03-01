namespace Tunnerer.Desktop.Rendering.Dx11.Shaders;

internal static class BlitPs
{
    public const string Source = @"
Texture2D t0 : register(t0);
SamplerState s0 : register(s0);

float4 main(float4 pos : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
{
    return t0.Sample(s0, uv);
}";
}
