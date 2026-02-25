#pragma once

#include "types.h"
#include <vector>

namespace crust
{

template <typename T>
class vector_2d : public std::vector<T>
{
    using base = std::vector<T>;
    Size dimension;

  public:
    vector_2d(Size dimensions) : size(dimensions) { resize(dimensions); }
    template <typename Initializer>
    vector_2d(Size dimensions, Initializer initializer) : dimension(dimensions)
    {
        for (int i = 0; i < dimensions.Area(); ++i)
        {
            this->push_back(initializer());
        }
    }
    constexpr Size size() const { return dimension; }
    constexpr T & get(Offset offset)
    {
        assert(dimension.FitsInside(offset));
        return this->operator[](offset.y * dimension.x + offset.x);
    }
    constexpr const T & get(Offset offset) const
    {
        assert(dimension.FitsInside(offset));
        return this->operator[](offset.y * dimension.x + offset.x);
    }
    constexpr void set(Offset offset, const T & item)
    {
        assert(dimension.FitsInside(offset));
        this[offset.y * dimension.x + offset.x] = item;
    }
    void resize(Size dimensions) { base::resize(dimensions.Area()); }
};

} // namespace crust