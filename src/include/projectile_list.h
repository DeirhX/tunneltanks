#pragma once
#include "containers.h"
#include "projectile.h"

class ProjectileList
{
    MultiTypeContainer<Bullet, Shrapnel> items;               /* Live items. Unmodified except for BEFORE Advance */
    MultiTypeContainer<Bullet, Shrapnel> newly_created_items; /* Items here will be integrated into main vector on Advance */
  public:
    ProjectileList() = default;
    template <typename TProjectile>
    TProjectile & Add(TProjectile&& projectile)
    {
        return this->newly_created_items.Add(projectile);
    }
    template <typename TProjectile>
    void Add(std::vector<TProjectile>&& array)
    {
        for (auto & projectile : array)
            this->Add(projectile);
    }

    void Remove(Projectile & projectile) { projectile.Invalidate(); }
    void Shrink();

    void Advance(class Level * level, class TankList * tankList);
    void Erase(class DrawBuffer * drawBuffer, class Level * level);
    void Draw(class DrawBuffer * drawBuffer);
};
