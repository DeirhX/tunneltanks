#pragma once

#include <types.h>

class Raycaster
{
    /* Visit all pixels through which a ray cast between two positions would go */
    template <typename TVisit>
    static void Cast(PositionF from, PositionF to, TVisit visitor)
    {
        OffsetF offset = to - from;
        /* Compute lowest simulation step that will advance a maximum of one pixel per step */
        float simulation_steps = std::ceil(static_cast<int>(offset.GetSize()));
        OffsetF one_step = offset / simulation_steps;
        PositionF curr_pos = from;
        int steps = static_cast<int>(simulation_steps);

        /* Perform the simulation */
        for (int i = 0; i <= steps; ++i)
        {
            PositionF new_pos = curr_pos + one_step;
            const auto prev = curr_pos.ToIntPosition();
            const auto next = new_pos.ToIntPosition();
            /* We might be still at the same pixel */
            if (prev == next)
                continue;
            /* Check if we went over the edge of one more pixel*/
            if (prev.x != next.x && prev.y != next.y)
            {
                /* Detect the touched pixel and visit it as well */
                Position touched_pos;
                if (std::abs(one_step.x) >= std::abs(one_step.y))
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
                visitor(touched_pos);
            }
            /* Coords changed, visit this pixel*/
            visitor(new_pos);
        }
    }
};
