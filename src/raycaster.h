#pragma once

#include <types.h>

class Raycaster
{
public:
    enum class VisitFlags
    {
        PixelsMustTouchCorners = 0x0,
        PixelsMustTouchSides = 0x1,
    };
    /* Visit all pixels through which a ray cast between two positions would go */
    /* visit_partial: PixelsMustTouchCorners - visited pixels can touch diagonally or side-by-side
     *                PixelsMustTouchSides  - include more pixels so that visited pixels must always side (exception: direct diagonal direction)
     */
    template <typename TVisit>
    static bool Cast(PositionF from, PositionF to, TVisit visitor, VisitFlags flags = VisitFlags::PixelsMustTouchSides) /* Visitor(PositionF tested_pos, PositionF previous_pos) -> bool should continue? */
    {
        if (from == to)
        {
            return visitor(to, to);
        }
        OffsetF offset = to - from;
        /* Compute lowest simulation step that will advance a maximum of one pixel per step */
        int steps = int(std::max(std::abs(offset.x), std::abs(offset.y)));
        OffsetF one_step = offset / float(steps);
        PositionF curr_pos = from;
        //int steps = static_cast<int>(std::round(std::max(std::abs(offset.x), std::abs(offset.y)) / std::max(std::abs(one_step.x), std::abs(one_step.y))));

        if (steps == 0)
            return visitor(to, from);

        /* Perform the simulation */
        for (int i = 1; i <= steps; ++i)
        {
            PositionF new_pos = curr_pos + one_step;
            const auto prev = curr_pos.ToIntPosition();
            const auto next = new_pos.ToIntPosition();
            /* We might be still at the same pixel */
            if (prev == next)
            {
                curr_pos = new_pos;
                continue;
            }
            /* Check if we went over the edge of one more pixel. Do this only if visit_partial was requested */
            if (HasFlag(flags, VisitFlags::PixelsMustTouchSides) && prev.x != next.x && prev.y != next.y &&
                std::abs(one_step.x) != std::abs(one_step.y))
            {
                /* Detect the touched pixel and visit it as well */
                Position touched_pos;
                if (std::abs(one_step.x) > std::abs(one_step.y))
                {
                    if (one_step.x >= 0)
                        touched_pos = prev + Offset{1, 0};
                    else
                        touched_pos = prev + Offset{-1, 0};
                }
                else
                {
                    if (one_step.y >= 0)
                        touched_pos = prev + Offset{0, 1};
                    else
                        touched_pos = prev + Offset{0, -1};
                }
                /* TODO: touched_pos is integer-based so it is not really on the correct float trajectory
                 * However, we will stop the simulation here only if the host signals a collision
                 *  and then previous position will usually be used as explosion source.
                 * Correct position can be computed via (1-(coord%1.0f)) * (one_step/one_step.coord)
                 */
                if (!visitor(PositionF{touched_pos}, curr_pos))
                    return false;
            }
            /* Coords changed, visit this pixel*/
            if (!visitor(new_pos, curr_pos))
                return false;
            curr_pos = new_pos;
        }
        return true;
    }
};
