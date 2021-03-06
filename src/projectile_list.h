#pragma once
#include "containers.h"
#include "projectiles.h"

class ProjectileList
{
    using ProjectileContainer =  MultiTypeContainer<Bullet, Shrapnel, ConcreteBarrel, ConcreteFoam, DirtBarrel, DirtFoam>;
    /* Live items. Unmodified except for BEFORE Advance */
    ProjectileContainer items;
    /* Items here will be integrated into main vector on Advance */
    ProjectileContainer newly_created_items;

  public:
    ProjectileList() = default;

    template <typename TProjectile>
    TProjectile & Add(TProjectile && projectile)
    {
        return this->newly_created_items.Add(std::forward<TProjectile>(projectile));
    }
    template <typename TProjectile>
    void Add(std::vector<TProjectile> && array)
    {
        for (auto && projectile : array)
            this->Add(std::move(projectile)); /* We're evicting this temporary array */
    }

    void Remove(Projectile & projectile) { projectile.Invalidate(); }
    void RemoveAll() { items.RemoveAll(); };
    void Shrink() { this->items.Shrink(); }

    void Advance(class Terrain * level, class TankList * tankList);
    void Draw(class Surface * drawBuffer);
};
