#pragma once
#include "containers.h"
#include "tank.h"
#include "types.h"

class ProjectileList;
class TankBase;

class TankList
{
  private:
    ValueContainer<Tank> list;
    Level * level;
    ProjectileList * projectile_list;

  public:
    TankList(Level * level, ProjectileList * projectile_list);
    //~TankList();

    Tank * AddTank(TankColor id, TankBase * tank_base);
    void RemoveTank(TankColor id);
    void RemoveAll();
    Tank * GetTankAtPoint(Position query_pos, TankColor ignored = -1);
    bool CheckForCollision(Tank & tank, Position testPos, int testDirection);

    // iterable
    decltype(list)::iterator begin() { return list.begin(); }
    decltype(list)::iterator end() { return list.end(); }

    template <typename TFunc>
    inline void for_each(TFunc && tank_func)
    {
        for (auto & tank : *this)
            tank_func(&tank);
    }
};
