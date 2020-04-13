#pragma once
#include "level_pixel.h"
#include "shape_renderer.h"
#include "types.h"
#include <optional>

namespace level
{

template <typename SurfaceType, typename PixelCompareFunc>
std::optional<Position> GetClosestPixel(const SurfaceType & surface, Position origin, int max_radius,
                                        PixelCompareFunc compare_func)
{
    float nearest_distance = std::numeric_limits<float>::max();
    Position nearest_pos = origin;

    auto check_pixel = [origin, max_radius, compare_func, &nearest_distance, &nearest_pos](Position pos, const LevelPixel & pixel) {
        if (compare_func(pixel))
        {
            const float distance = (pos - origin).GetSize();
            if (distance <= float(max_radius) && nearest_distance > distance)
            {
                nearest_pos = pos;
                nearest_distance = distance;
            }
        }
        return true;
    };

    for (int i = 1; i < max_radius; ++i)
    {
        ShapeInspector::InspectRectangle(surface, Rect{origin.x - i, origin.y - i, i * 2 + 1, i * 2 + 1}, check_pixel);
        if (float(i) >= nearest_distance)
            break;
    }

    if (nearest_pos != origin)
    {
        return nearest_pos;
    }
    return {};
}

} // namespace level