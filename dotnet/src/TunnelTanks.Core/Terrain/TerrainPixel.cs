namespace TunnelTanks.Core.Terrain;

using TunnelTanks.Core.Types;

public enum TerrainPixel : byte
{
    LevelGenDirt = 0,
    LevelGenRock = 1,
    LevelGenMark = 2,

    Blank = (byte)' ',
    DirtHigh = (byte)'D',
    DirtLow = (byte)'d',
    DirtGrow = (byte)'g',
    Rock = (byte)'r',
    DecalHigh = (byte)'.',
    DecalLow = (byte)',',
    BaseMin = (byte)'0',
    BaseMax = (byte)'7',
    BaseBarrier = (byte)'8',
    ConcreteLow = (byte)'c',
    ConcreteHigh = (byte)'C',
    EnergyLow = (byte)'e',
    EnergyMedium = (byte)'E',
    EnergyHigh = (byte)'F',
}

public static class Pixel
{
    public static bool IsDirt(TerrainPixel p) => p == TerrainPixel.DirtHigh || p == TerrainPixel.DirtLow;
    public static bool IsDiggable(TerrainPixel p) => p == TerrainPixel.DirtHigh || p == TerrainPixel.DirtLow || p == TerrainPixel.DirtGrow;
    public static bool IsTorchable(TerrainPixel p) => IsDiggable(p) || IsMineral(p);
    public static bool IsSoftCollision(TerrainPixel p) => IsDirt(p);
    public static bool IsBlockingCollision(TerrainPixel p) => p == TerrainPixel.Rock || IsConcrete(p) || IsBase(p);
    public static bool IsAnyCollision(TerrainPixel p) => IsSoftCollision(p) || IsBlockingCollision(p);
    public static bool IsBase(TerrainPixel p) => p >= TerrainPixel.BaseMin && p <= TerrainPixel.BaseMax;
    public static bool IsScorched(TerrainPixel p) => p == TerrainPixel.DecalHigh || p == TerrainPixel.DecalLow;
    public static bool IsConcrete(TerrainPixel p) => p == TerrainPixel.ConcreteHigh || p == TerrainPixel.ConcreteLow;
    public static bool IsRock(TerrainPixel p) => p == TerrainPixel.Rock;
    public static bool IsMineral(TerrainPixel p) => IsConcrete(p) || IsRock(p);
    public static bool IsEnergy(TerrainPixel p) => p == TerrainPixel.EnergyLow || p == TerrainPixel.EnergyMedium || p == TerrainPixel.EnergyHigh;
    public static bool IsEmpty(TerrainPixel p) => p == TerrainPixel.Blank;

    public static Color GetColor(TerrainPixel p) => p switch
    {
        TerrainPixel.Blank => new Color(0x00, 0x00, 0x00),
        TerrainPixel.DirtHigh => new Color(0xc3, 0x79, 0x30),
        TerrainPixel.DirtLow => new Color(0xba, 0x59, 0x04),
        TerrainPixel.DirtGrow => new Color(0x6a, 0x29, 0x02),
        TerrainPixel.Rock => new Color(0x9a, 0x9a, 0x9a),
        TerrainPixel.DecalHigh => new Color(0x48, 0x38, 0x2f),
        TerrainPixel.DecalLow => new Color(0x28, 0x28, 0x28),
        TerrainPixel.ConcreteLow => new Color(0xa0, 0xa0, 0xa5),
        TerrainPixel.ConcreteHigh => new Color(0x80, 0x80, 0x85),
        TerrainPixel.EnergyLow => new Color(0xa0, 0xa0, 0x19),
        TerrainPixel.EnergyMedium => new Color(0xd0, 0xd0, 0x30),
        TerrainPixel.EnergyHigh => new Color(0xff, 0xff, 0x4a),
        _ when Pixel.IsBase(p) => new Color(0x40, 0x40, 0x40),
        _ => new Color(0xff, 0x00, 0xff),
    };
}
