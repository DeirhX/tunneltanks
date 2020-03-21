#include "base.h"
#include <cstdio>

#include <level.h>
#include <projectile.h>
#include <random.h>
#include <tank.h>
#include <tweak.h>

#include "tanklist.h"
#include "raycaster.h"

void Bullet::Advance(TankList * tankList)
{
    auto IteratePositions = [this, tankList](PositionF tested_pos, PositionF prev_pos)
    {
        this->pos = tested_pos;
        this->pos_blur_from = prev_pos;

        /* Did we hit another tank? */
        TankColor hitTankColor = this->tank->GetColor();
        Tank * hitTank = tankList->GetTankAtPoint(this->pos.ToIntPosition(), hitTankColor);
        if (hitTank)
        {
            /* If we have an associated tank, return the shot: */
            this->tank->ReturnBullet();

            /* Hurt the tank we hit: */
            hitTank->AlterHealth(tweak::tank::ShotDamage);

            /* Add all of the effect particles: */

            for (Shrapnel & shrapnel :
                 Explosion::Explode(this->pos_blur_from.ToIntPosition(), level, EXPLOSION_HURT_COUNT,
                                    EXPLOSION_HURT_RADIUS, EXPLOSION_HURT_TTL))
            {
                tankList->projectile_list->Add(shrapnel);
            }
            /* Finally, remove it: */
            this->Invalidate();
            return false;
        }

        /* Else, did we hit something in the level? */
        LevelVoxel c = level->GetVoxel(this->pos.ToIntPosition());
        if (Voxels::IsAnyCollision(c))
        {
            /* If we have an associated tank, return the shot: */
            this->tank->ReturnBullet();

            for (Shrapnel & shrapnel :
                 Explosion::Explode(this->pos_blur_from.ToIntPosition(), level, EXPLOSION_DIRT_COUNT,
                                    EXPLOSION_DIRT_RADIUS, EXPLOSION_DIRT_TTL))
            {
                tankList->projectile_list->Add(shrapnel);
            }
            /* Finally, remove it: */
            this->Invalidate();
            return false;
        }

        return true;
    };
    Raycaster::Cast(this->pos, this->pos + (this->speed * float(this->simulation_steps)), IteratePositions);
}

void Bullet::Draw(DrawBuffer * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::FireHot));
    drawBuffer->SetPixel(this->pos_blur_from.ToIntPosition(), Palette.Get(Colors::FireCold));
}

void Bullet::Erase(DrawBuffer * drawBuffer, Level *)
{
    level->CommitPixel(this->pos.ToIntPosition());
    level->CommitPixel(this->pos_blur_from.ToIntPosition());
}

void Shrapnel::Advance(TankList * tankList)
{
    /* Did this expire? */
    if (!this->simulation_steps)
    {
        this->Invalidate();
        return;
    }

    /* Move the effect: */
    this->simulation_steps--;
    this->pos.x += this->speed.x;
    this->pos.y += this->speed.y;

    /* Make sure we didn't hit a level detail: */
    LevelVoxel c = level->GetVoxel(this->pos.ToIntPosition());
    if (Voxels::IsBlockingCollision(c))
    {
        this->Invalidate();
        return;
    }

    /* Effects blank everything out in their paths: */
    level->SetVoxel(this->pos.ToIntPosition(), Random.Bool(500) ? LevelVoxel::DecalHigh : LevelVoxel::DecalLow);
}

void Shrapnel::Draw(DrawBuffer * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::FireHot));
}

void Shrapnel::Erase(DrawBuffer * drawBuffer, Level *)
{
    level->CommitPixel(this->pos.ToIntPosition());
}

std::vector<Shrapnel> Explosion::Explode(Position pos, Level *level, int count, int radius, int ttl)
{
    /*  Beware. Explosions use multiplied positions to maintain 'floating' fraction as they move less than one pixel
     * between frames */
    auto items = std::vector<Shrapnel>{};
    items.reserve(count);
    /* Add all of the effect particles: */
    for (int i = 0; i < count; i++)
    {
        items.emplace_back(
            Shrapnel{Position{pos.x, pos.y},
                     SpeedF{Random.Float(-radius / 32.f, radius / 32.f), Random.Float(-radius / 32.f, radius / 32.f)},
                       Random.Int(0, ttl), level});
    }
    return items;
}
