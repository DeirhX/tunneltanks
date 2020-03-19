#pragma once
#include "containers.h"
#include "projectile.h"

class ProjectileList
{
    ValueContainer<Bullet> bullets;
    ValueContainer<Shrapnel> shrapnels;

  public:
    Bullet & Add(Bullet projectile) { return this->bullets.Add(projectile); }
    Shrapnel & Add(Shrapnel projectile) { return this->shrapnels.Add(projectile); }
    void Add(std::vector<Shrapnel> array);

    void Remove(Projectile & projectile) { projectile.Invalidate(); }
    void Shrink();

    void Advance(class Level * level, class TankList * tankList);
    void Erase(class DrawBuffer * drawBuffer, class Level * level);
    void Draw(class DrawBuffer * drawBuffer);
};
