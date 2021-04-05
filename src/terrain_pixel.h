#pragma once

/*
 * TerrainPixel
 * Core pixel state of the level. Terrain, bases, rock and every other pixel kind there is.
 */
enum class TerrainPixel : char
{
    Blank = ' ',     /* Nothing. The void of the space. */
    DirtHigh = 'D',  /* Standard dirt */
    DirtLow = 'd',   /* Standard dirt */
    DirtGrow = 'g',  /* Regrowing dirt, not yet collidable */
    Rock = 'r',      /* Indestructible (almost) */
    DecalHigh = '.', /* Decal after explosion. Harder to regrow */
    DecalLow = ',',  /* Decal after explosion. Harder to regrow */
    BaseMin = '0',   /* Tank Base. Goes up to '7' for various tank colors */
    BaseMax = '7',
    BaseBarrier = '8',  /* Invisible base gates. Only blocks wild growth. */
    ConcreteLow = 'c',  /* Hardened concrete, tough to destroy */
    ConcreteHigh = 'C', /* Hardened concrete, tough to destroy */
    EnergyLow = 'e',    /* Raw energy, harvestable by tanks */
    EnergyMedium = 'E', /* More raw energy, harvestable by tanks */
    EnergyHigh = 'F',   /* A lot of raw energy, harvestable by tanks */

    LevelGenDirt = 0,
    LevelGenRock = 1,
    LevelGenMark = 2,
};
/*
 * Queries that can be made against the pixel to classify it into various groups 
 */
class Pixel
{
  public:
    static bool IsDirt(TerrainPixel pixel) { return pixel == TerrainPixel::DirtHigh || pixel == TerrainPixel::DirtLow; }
    static bool IsDiggable(TerrainPixel pixel)
    {
        return pixel == TerrainPixel::DirtHigh || pixel == TerrainPixel::DirtLow || pixel == TerrainPixel::DirtGrow;
    }
    static bool IsTorchable(TerrainPixel pixel) { return IsDiggable(pixel) || IsMineral(pixel); }
    static bool IsSoftCollision(TerrainPixel pixel) { return IsDirt(pixel); }
    static bool IsBlockingCollision(TerrainPixel pixel)
    {
        return pixel == TerrainPixel::Rock || IsConcrete(pixel) ||
               (pixel >= TerrainPixel::BaseMin && pixel <= TerrainPixel::BaseMax);
    }
    static bool IsAnyCollision(TerrainPixel pixel) { return IsSoftCollision(pixel) || IsBlockingCollision(pixel); }
    static bool IsBase(TerrainPixel pixel) { return (pixel >= TerrainPixel::BaseMin && pixel <= TerrainPixel::BaseMax); }
    static bool IsScorched(TerrainPixel pixel) { return pixel == TerrainPixel::DecalHigh || pixel == TerrainPixel::DecalLow; }
    static bool IsConcrete(TerrainPixel pixel)
    {
        return pixel == TerrainPixel::ConcreteHigh || pixel == TerrainPixel::ConcreteLow;
    }
    static bool IsRock(TerrainPixel pixel) { return pixel == TerrainPixel::Rock;  }
    static bool IsMineral(TerrainPixel pixel) { return IsConcrete(pixel) || IsRock(pixel); }
    static bool IsEnergy(TerrainPixel pixel)
    {
        return pixel == TerrainPixel::EnergyLow || pixel == TerrainPixel::EnergyMedium || pixel == TerrainPixel::EnergyHigh;
    }
    static bool IsEmpty(TerrainPixel pixel) { return pixel == TerrainPixel::Blank; }
};
