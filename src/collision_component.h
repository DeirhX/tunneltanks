#pragma once
#include <memory>
#include <vector>
#include <array>
#include <span>
#include "types.h"

namespace crust::components
{
    class BitmapCollision
    {
    private:
        Size size;
        Offset center;
        bool multiple_directions = false;
        std::vector<uint8_t> collision_data;
    private:

    public:
        BitmapCollision(Size size, Offset center, std::span<uint8_t> collision_data);
      BitmapCollision(Size size, Offset center, std::span<std::span<uint8_t>, 9> multiple_directions);

        [[nodiscard]]
        Size Size() const { return this->size; }

        std::span<const uint8_t> GetForDirection(Direction direction) const;
        std::span<uint8_t> GetForDirection(Direction direction);

        uint8_t GetRelative(Offset offset) const 
        {
            Offset data_offset = offset + center;
            if (!size.FitsInside(data_offset))
            {
                return {0};
            }
            return collision_data[data_offset.x + data_offset.y * size.x];
        }

        //static std::span<std::span<std::byte>, 9> RawCharsToDirectionalSpans(::Size size, const char * unsafeArray);
    };

}
