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
    static bool IsDirt(LevelPixel voxel) { return voxel == LevelPixel::DirtHigh || voxel == LevelPixel::DirtLow; }
    static bool IsDiggable(LevelPixel voxel)
    {
        return voxel == LevelPixel::DirtHigh || voxel == LevelPixel::DirtLow || voxel == LevelPixel::DirtGrow;
    }
    static bool IsSoftCollision(LevelPixel voxel) { return IsDirt(voxel); }
    static bool IsBlockingCollision(LevelPixel voxel)
    {
        return voxel == LevelPixel::Rock || IsConcrete(voxel) ||
               (voxel >= LevelPixel::BaseMin && voxel <= LevelPixel::BaseMax);
    }
    static bool IsAnyCollision(LevelPixel voxel) { return IsSoftCollision(voxel) || IsBlockingCollision(voxel); }
    static bool IsBase(LevelPixel voxel) { return (voxel >= LevelPixel::BaseMin && voxel <= LevelPixel::BaseMax); }
    static bool IsScorched(LevelPixel voxel) { return voxel == LevelPixel::DecalHigh || voxel == LevelPixel::DecalLow; }
    static bool IsConcrete(LevelPixel voxel)
    {
        return voxel == LevelPixel::ConcreteHigh || voxel == LevelPixel::ConcreteLow;
    }
    static bool IsRock(LevelPixel pixel) { return pixel == LevelPixel::Rock;  }
};
