namespace Tunnerer.Core.Terrain;

using Tunnerer.Core.Types;

public enum TerrainPixel : byte
{
    LevelGenDirt = 0,
    LevelGenRock = 1,
    LevelGenMark = 2,

    Blank = (byte)' ',
    DirtHigh = (byte)'D',
    DirtGrow = (byte)'g',
    Rock = (byte)'r',
    DecalHigh = (byte)'.',
    DecalLow = (byte)',',
    BaseMin = (byte)'0',
    BaseMax = (byte)'7',
    BaseBarrier = (byte)'8',
    BaseCore = (byte)'9',
    ConcreteLow = (byte)'c',
    ConcreteHigh = (byte)'C',
    EnergyLow = (byte)'e',
    EnergyMedium = (byte)'E',
    EnergyHigh = (byte)'F',
}

public enum ThermalMaterial : byte
{
    Air = 0,
    Dirt = 1,
    Stone = 2,
    Base = 3,
    ConstantEnergy = 4,
}

/// <summary>
/// Single source of truth for terrain classification.
/// Adding a new terrain pixel = one entry in <see cref="Pixel.BuildBehaviorTable"/>.
/// All <c>Pixel.Is*</c> methods delegate to this table.
/// </summary>
public readonly record struct TerrainBehavior(
    bool BlocksMovement,
    bool SoftCollision,
    bool Diggable,
    bool Concrete,
    bool Mineral,
    bool Energy,
    bool Base,
    bool Scorched,
    Color DisplayColor)
{
    public bool IsAnyCollision => BlocksMovement || SoftCollision;
    public bool Torchable => Diggable || Mineral;
    public bool Rock => Mineral && !Concrete;
    public bool Dirt => SoftCollision;
}

public static class Pixel
{
    private static readonly TerrainBehavior[] _behaviors = BuildBehaviorTable();

    private static readonly Color MagentaFallback = new(0xff, 0x00, 0xff);

    private static TerrainBehavior[] BuildBehaviorTable()
    {
        var table = new TerrainBehavior[256];

        void Set(TerrainPixel p, TerrainBehavior b) => table[(int)p] = b;

        Set(TerrainPixel.Blank, new(
            BlocksMovement: false, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: false, Energy: false, Base: false, Scorched: false,
            DisplayColor: new Color(0x00, 0x00, 0x00)));
        Set(TerrainPixel.DirtHigh, new(
            BlocksMovement: false, SoftCollision: true, Diggable: true,
            Concrete: false, Mineral: false, Energy: false, Base: false, Scorched: false,
            DisplayColor: new Color(0xc3, 0x79, 0x30)));
        Set(TerrainPixel.DirtGrow, new(
            BlocksMovement: false, SoftCollision: false, Diggable: true,
            Concrete: false, Mineral: false, Energy: false, Base: false, Scorched: false,
            DisplayColor: new Color(0x6a, 0x29, 0x02)));
        Set(TerrainPixel.Rock, new(
            BlocksMovement: true, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: true, Energy: false, Base: false, Scorched: false,
            DisplayColor: new Color(0x9a, 0x9a, 0x9a)));
        Set(TerrainPixel.DecalHigh, new(
            BlocksMovement: false, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: false, Energy: false, Base: false, Scorched: true,
            DisplayColor: new Color(0x48, 0x38, 0x2f)));
        Set(TerrainPixel.DecalLow, new(
            BlocksMovement: false, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: false, Energy: false, Base: false, Scorched: true,
            DisplayColor: new Color(0x28, 0x28, 0x28)));
        Set(TerrainPixel.ConcreteLow, new(
            BlocksMovement: true, SoftCollision: false, Diggable: false,
            Concrete: true, Mineral: true, Energy: false, Base: false, Scorched: false,
            DisplayColor: new Color(0xa0, 0xa0, 0xa5)));
        Set(TerrainPixel.ConcreteHigh, new(
            BlocksMovement: true, SoftCollision: false, Diggable: false,
            Concrete: true, Mineral: true, Energy: false, Base: false, Scorched: false,
            DisplayColor: new Color(0x80, 0x80, 0x85)));
        Set(TerrainPixel.EnergyLow, new(
            BlocksMovement: false, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: false, Energy: true, Base: false, Scorched: false,
            DisplayColor: new Color(0xa0, 0xa0, 0x19)));
        Set(TerrainPixel.EnergyMedium, new(
            BlocksMovement: false, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: false, Energy: true, Base: false, Scorched: false,
            DisplayColor: new Color(0xd0, 0xd0, 0x30)));
        Set(TerrainPixel.EnergyHigh, new(
            BlocksMovement: false, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: false, Energy: true, Base: false, Scorched: false,
            DisplayColor: new Color(0xff, 0xff, 0x4a)));
        Set(TerrainPixel.BaseBarrier, new(
            BlocksMovement: false, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: false, Energy: false, Base: false, Scorched: false,
            DisplayColor: new Color(0x40, 0x40, 0x40)));
        Set(TerrainPixel.BaseCore, new(
            BlocksMovement: false, SoftCollision: false, Diggable: false,
            Concrete: false, Mineral: false, Energy: false, Base: false, Scorched: false,
            DisplayColor: new Color(0x20, 0x20, 0x20)));

        var baseColor = new Color(0x40, 0x40, 0x40);
        for (byte b = (byte)TerrainPixel.BaseMin; b <= (byte)TerrainPixel.BaseMax; b++)
            table[b] = new(
                BlocksMovement: true, SoftCollision: false, Diggable: false,
                Concrete: false, Mineral: false, Energy: false, Base: true, Scorched: false,
                DisplayColor: baseColor);

        return table;
    }

    public static ref readonly TerrainBehavior GetBehavior(TerrainPixel p) => ref _behaviors[(int)p];

    public static bool IsDirt(TerrainPixel p) => _behaviors[(int)p].Dirt;
    public static bool IsDiggable(TerrainPixel p) => _behaviors[(int)p].Diggable;
    public static bool IsTorchable(TerrainPixel p) => _behaviors[(int)p].Torchable;
    public static bool IsSoftCollision(TerrainPixel p) => _behaviors[(int)p].SoftCollision;
    public static bool IsBlockingCollision(TerrainPixel p) => _behaviors[(int)p].BlocksMovement;
    public static bool IsAnyCollision(TerrainPixel p) => _behaviors[(int)p].IsAnyCollision;
    public static bool IsBase(TerrainPixel p) => _behaviors[(int)p].Base;
    public static bool IsScorched(TerrainPixel p) => _behaviors[(int)p].Scorched;
    public static bool IsConcrete(TerrainPixel p) => _behaviors[(int)p].Concrete;
    public static bool IsRock(TerrainPixel p) => _behaviors[(int)p].Rock;
    public static bool IsMineral(TerrainPixel p) => _behaviors[(int)p].Mineral;
    public static bool IsEnergy(TerrainPixel p) => _behaviors[(int)p].Energy;
    public static bool IsEmpty(TerrainPixel p) => p == TerrainPixel.Blank;

    public static ThermalMaterial GetThermalMaterial(TerrainPixel p)
    {
        var b = _behaviors[(int)p];
        if (p == TerrainPixel.BaseBarrier) return ThermalMaterial.Base;
        if (p == TerrainPixel.BaseCore) return ThermalMaterial.ConstantEnergy;
        if (b.Base) return ThermalMaterial.Base;
        if (b.Energy) return ThermalMaterial.ConstantEnergy;
        if (b.Dirt) return ThermalMaterial.Dirt;

        // Hard terrain and structures behave as stone for thermal exchange.
        if (b.BlocksMovement || b.Concrete || b.Mineral)
            return ThermalMaterial.Stone;

        return ThermalMaterial.Air;
    }

    public static Color GetColor(TerrainPixel p)
    {
        var c = _behaviors[(int)p].DisplayColor;
        return c == default ? MagentaFallback : c;
    }
}
