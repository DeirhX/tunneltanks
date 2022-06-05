#pragma once
#include <memory>
#include <vector>
#include "types.h"

namespace crust::components
{
    class BitmapCollision
    {
    private:
        Size size;
        Offset center;
        std::vector<std::byte> collision_data;
    public:
        [[nodiscard]]
        Size Size() const { return this->size; }
        [[nodiscard]]
        std::byte GetRelative(Offset offset) const 
        {
            Offset data_offset = offset + center;
            if (!size.FitsInside(data_offset))
            {
                return std::byte{0};
            }
            return collision_data[data_offset.x + data_offset.y * size.x];
        }
    };

}
