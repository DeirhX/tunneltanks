float SampleLandformHeight(float2 worldCell)
{
    float broad = fbmNoise(worldCell * 0.0075 + float2(71.0, 911.0), 4);
    float folded = fbmNoise(float2(
        worldCell.x * 0.006 + worldCell.y * 0.002,
        -worldCell.x * 0.002 + worldCell.y * 0.006) + float2(313.0, 509.0), 3);
    return saturate(broad * 0.62 + folded * 0.58);
}
