#pragma once
#include "containers.h"
#include "level_pixel.h"

/*
 * Container for raw level data
 */
class RawLevelData
{
    using Container = std::vector<LevelPixel>;
    Container array;

  public:
    RawLevelData(Size size) : array(size.x * size.y) {}

    LevelPixel & operator[](int i) { return array[i]; }
    const LevelPixel & operator[](int i) const { return array[i]; }

    //LevelPixel GetLevelData(Position pos);
    //LevelPixel GetLevelData(int offset) { return operator[](offset); }

    Container::iterator begin() { return array.begin(); }
    Container::iterator end() { return array.end(); }
    Container::const_iterator cbegin() const { return array.cbegin(); }
    Container::const_iterator cend() const { return array.cend(); }
};