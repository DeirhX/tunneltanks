#pragma once

#include "types.h"
#include <vector>

namespace crust
{

template <typename T>
class vector_2d : public std::vector<T>
{
    using base = std::vector<T>;
    Size size;

  public:
    vector_2d(Size dimensions) : size(dimensions) { resize(dimensions); }
    template <typename Initializer>
    vector_2d(Size dimensions, Initializer initializer) : size(dimensions)
    {
        for (int i = 0; i < dimensions.Area(); ++i)
        {
            this->push_back(initializer());
        }
    }
    T & get(Offset offset)
    {
        assert(size.FitsInside(offset));
        return this->operator[](offset.y * size.x + offset.x);
    }
    const T & get(Offset offset) const
    {
        assert(size.FitsInside(offset));
        return this->operator[](offset.y * size.x + offset.x);
    }
    void set(Offset offset, const T & item)
    {
        assert(size.FitsInside(offset));
        this[offset.y * size.x + offset.x] = item;
    }
    void resize(Size dimensions) { base::resize(dimensions.Area()); }
};

} // namespace crust