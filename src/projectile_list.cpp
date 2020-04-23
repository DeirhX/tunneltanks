#include "projectile_list.h"
#include "base.h"
#include "level.h"
#include "projectile.h"
#include "random.h"
#include "tank.h"
#include "tank_list.h"

void ProjectileList::Advance(Level * level, TankList * tankList)
{
    /* Append everything that was created last tick */
    this->items.MoveFrom(this->newly_created_items);
    Shrink();

    /* Advance everything */
    this->items.ForEach([tankList](auto & item) { item.Advance(tankList); });
}
void ProjectileList::Draw(Surface * drawBuffer)
{
    this->items.ForEach([drawBuffer](auto & item) { item.Draw(drawBuffer); });
}
