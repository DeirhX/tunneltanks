#pragma once
#include "containers.h"
#include "projectile.h"

class ProjectileList
{
    /* Live items. Unmodified except for BEFORE Advance */
    MultiTypeContainer<Bullet, Shrapnel, ConcreteBarrel, ConcreteFoam, DirtBarrel, DirtFoam> items;
    /* Items here will be integrated into main vector on Advance */
    MultiTypeContainer<Bullet, Shrapnel, ConcreteBarrel, ConcreteFoam, DirtBarrel, DirtFoam> newly_created_items;

  public:
    ProjectileList() = default;
    template <typename TProjectile>
    TProjectile & Add(TProjectile && projectile)
    {
        return this->newly_created_items.Add(projectile);
    }
    template <typename TProjectile>
    void Add(std::vector<TProjectile> && array)
    {
        for (auto & projectile : array)
            this->Add(projectile);
    }

    void Remove(Projectile & projectile) { projectile.Invalidate(); }
    void Shrink();

    void Advance(class Level * level, class TankList * tankList);
    void Draw(class Surface * drawBuffer);
};
