#include "base.h"
#include "projectile.h"
#include "projectile_list.h"
#include "level.h"
#include "random.h"
#include "colors.h"
#include "tank.h"
#include "tanklist.h"

void ProjectileList::Add(std::vector<Shrapnel> array)
{
    for (auto & shrapnel: array)
        this->Add(shrapnel);
}

void ProjectileList::Shrink()
{
    this->items.Shrink();
}

void ProjectileList::Advance(Level * level, TankList * tankList)
{
    /* Create copies to not be influenced by possible container modification*/
    this->items.ForEach([tankList](Projectile & item) { item.Advance(tankList); });

    Shrink();
}

void ProjectileList::Erase(DrawBuffer* drawBuffer, Level* level)
{
    this->items.ForEach([drawBuffer, level](Projectile & item) { item.Erase(drawBuffer, level); });
}


void ProjectileList::Draw(DrawBuffer* drawBuffer)
{
    this->items.ForEach([drawBuffer](Projectile & item) { item.Draw(drawBuffer); });
}
