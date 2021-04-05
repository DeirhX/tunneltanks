#pragma once

/*
 * LevelPixel
 * Core pixel state of the level. Terrain, bases, rock and every other pixel kind there is.
 */
enum class LevelPixel : char
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
    static bool IsDirt(LevelPixel pixel) { return pixel == LevelPixel::DirtHigh || pixel == LevelPixel::DirtLow; }
    static bool IsDiggable(LevelPixel pixel)
    {
        return pixel == LevelPixel::DirtHigh || pixel == LevelPixel::DirtLow || pixel == LevelPixel::DirtGrow;
    }
    static bool IsTorchable(LevelPixel pixel) { return IsDiggable(pixel) || IsMineral(pixel); }
    static bool IsSoftCollision(LevelPixel pixel) { return IsDirt(pixel); }
    static bool IsBlockingCollision(LevelPixel pixel)
    {
        return pixel == LevelPixel::Rock || IsConcrete(pixel) ||
               (pixel >= LevelPixel::BaseMin && pixel <= LevelPixel::BaseMax);
    }
    static bool IsAnyCollision(LevelPixel pixel) { return IsSoftCollision(pixel) || IsBlockingCollision(pixel); }
    static bool IsBase(LevelPixel pixel) { return (pixel >= LevelPixel::BaseMin && pixel <= LevelPixel::BaseMax); }
    static bool IsScorched(LevelPixel pixel) { return pixel == LevelPixel::DecalHigh || pixel == LevelPixel::DecalLow; }
    static bool IsConcrete(LevelPixel pixel)
    {
        return pixel == LevelPixel::ConcreteHigh || pixel == LevelPixel::ConcreteLow;
    }
    static bool IsRock(LevelPixel pixel) { return pixel == LevelPixel::Rock;  }
    static bool IsMineral(LevelPixel pixel) { return IsConcrete(pixel) || IsRock(pixel); }
    static bool IsEnergy(LevelPixel pixel)
    {
        return pixel == LevelPixel::EnergyLow || pixel == LevelPixel::EnergyMedium || pixel == LevelPixel::EnergyHigh;
    }
    static bool IsEmpty(LevelPixel pixel) { return pixel == LevelPixel::Blank; }
};
