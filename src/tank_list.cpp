#include <cstdlib>

#include <level.h>
#include <projectile.h>
#include <tank.h>
#include <tank_list.h>
#include <tank_sprites.h>
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

void TankList::RemoveAll()
{ this->list.RemoveAll(); }

Tank * TankList::GetTankAtPoint(Position query_pos, TankColor ignored)
{
    for (Tank & tank : *this)
    {
        if (tank.GetColor() == ignored || tank.HealthOrEnergyEmpty())
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

bool TankList::CheckForCollision(Position position, Direction rotation, Tank & ignored_tank)
{
    for (Tank & otherTank : *this)
    {
        if (otherTank.GetColor() == ignored_tank.GetColor() || otherTank.HealthOrEnergyEmpty())
            continue;

        /* Let's see if we're ANYWHERE near this tank (bounding box basically) : */
        Position pos = otherTank.GetPosition();
        if (abs(position.x - pos.x) > 6 || abs(position.y - pos.y) > 6)
            continue;

        /* Ok, if we're here, the two tanks are real close. Now it's time for
         * brute-force pixel checking: */
        int dir = otherTank.GetDirection().ToIntDirection();

        /* Find the bounds of the two sprite's overlap: */
        int lx, ly, ux, uy;
        if(pos.x< position.x) { lx= position.x-3; ux= pos.x+3;   }
        else                 { lx= pos.x-3;     ux= position.x+3; }
        if(pos.y< position.y) { ly= position.y-3; uy= pos.y+3;   }
        else                 { ly= pos.y-3;     uy= position.y+3; }

        /* Check the overlap for collisions: */
        for (int ty = ly; ty <= uy; ty++)
            for (int tx = lx; tx <= ux; tx++)
                if (TANK_SPRITE[dir][ty - pos.y + 3][tx - pos.x + 3] &&
                    TANK_SPRITE[rotation][ty - position.y + 3][tx - position.x + 3])
                    return true;
    }

    return false;
}