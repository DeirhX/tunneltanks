#include "base.h"
#include <cstdlib>

#include <level.h>
#include <projectile.h>
#include <ranges>
#include <tank.h>
#include <tanklist.h>
#include <tanksprites.h>
#include <tweak.h>
#include <types.h>

#include "exceptions.h"
#include "projectile_list.h"
#include <algorithm>

TankList::TankList(Level * level, ProjectileList * projectile_list) : level(level), projectile_list(projectile_list) {}

Tank * TankList::AddTank(TankColor color, TankBase * tank_base)
{
    auto found = std::find_if(begin(), end(), [color](auto & el) { return color == el.GetColor(); });
    if (found != list.end())
        throw GameException("already exists");

    return &this->list.ConstructElement(color, this->level, this->projectile_list, tank_base);
}

void TankList::RemoveTank(TankColor color)
{
    auto found = std::find_if(begin(), end(), [color](auto & el) { return color == el.GetColor(); });
    (*found).Invalidate();
}

Tank * TankList::GetTankAtPoint(Position query_pos, TankColor ignored)
{
    for (Tank & tank : *this)
    {
        if (tank.GetColor() == ignored || tank.IsDead())
            continue;

        Position pos = tank.GetPosition();
        pos.x = query_pos.x - pos.x + 3;
        pos.y = query_pos.y - pos.y + 3;
        if (pos.x < 0 || pos.x > 6 || pos.y < 0 || pos.y > 6)
            continue; /* Early exit, outside bounding box*/

        if (TANK_SPRITE[tank.GetDirection().ToIntDirection()][pos.y][pos.x])
            return &tank;
    }
    return nullptr;
}

/* Note: change that vector to two int's eventually... */
bool TankList::CheckForCollision(Tank & tank, Position testPos, int testDirection)
{
    for (Tank & otherTank : *this)
    {
        if (otherTank.GetColor() == tank.GetColor() || otherTank.IsDead())
            continue;

        /* Let's see if these two tanks are ANYWHERE near each other: */
        Position pos = otherTank.GetPosition();
        if (abs(testPos.x - pos.x) > 6 || abs(testPos.y - pos.y) > 6)
            continue;

        /* Ok, if we're here, the two tanks are real close. Now it's time for
         * brute-force pixel checking: */
        int dir = otherTank.GetDirection().ToIntDirection();

        /* Find the bounds of the two sprite's overlap: */
        int lx, ly, ux, uy;
        if(pos.x< testPos.x) { lx= testPos.x-3; ux= pos.x+3;   }
        else                 { lx= pos.x-3;     ux= testPos.x+3; }
        if(pos.y< testPos.y) { ly= testPos.y-3; uy= pos.y+3;   }
        else                 { ly= pos.y-3;     uy= testPos.y+3; }

        /* Check the overlap for collisions: */
        for (int ty = ly; ty <= uy; ty++)
            for (int tx = lx; tx <= ux; tx++)
                if (TANK_SPRITE[dir][ty - pos.y + 3][tx - pos.x + 3] &&
                    TANK_SPRITE[testDirection][ty - testPos.y + 3][tx - testPos.x + 3])
                    return true;
    }

    return false;
}