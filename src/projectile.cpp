#include "base.h"
#include <cstdio>

#include <level.h>
#include <projectile.h>
#include <random.h>
#include <tank.h>
#include <tweak.h>

#include "tanklist.h"

void Bullet::Advance(TankList * tankList)
{
    for (int i = 0; i < this->simulation_steps; i++)
    {
        this->pos_blur_from.x = this->pos.x;
        this->pos_blur_from.y = this->pos.y;
        this->pos.x += this->speed.x;
        this->pos.y += this->speed.y;

        /* Did we hit another tank? */
        int hitTankColor = this->tank->GetColor();
        Tank * hitTank = tankList->GetTankAtPoint(this->pos.x, this->pos.y, hitTankColor);
        if (hitTank)
        {
            /* If we have an associated tank, return the shot: */
            this->tank->ReturnBullet();

            /* Hurt the tank we hit: */
            hitTank->AlterHealth(tweak::tank::ShotDamage);

            /* Add all of the effect particles: */
            

            for (Shrapnel & shrapnel :
                 Explosion::Explode(Position{this->pos_blur_from.x, this->pos_blur_from.y}, level, EXPLOSION_HURT_COUNT,
                                    EXPLOSION_HURT_RADIUS, EXPLOSION_HURT_TTL))
            {
                tankList->projectile_list->Add(shrapnel);
            }
            /* Finally, remove it: */
            this->Invalidate();
            return;
        }

        /* Else, did we hit something in the level? */
        LevelVoxel c = level->GetVoxel(this->pos);
        if (Voxels::IsAnyCollision(c))
        {
            /* If we have an associated tank, return the shot: */
            this->tank->ReturnBullet();

            for (Shrapnel & shrapnel :
                 Explosion::Explode(Position{this->pos_blur_from.x, this->pos_blur_from.y}, level, EXPLOSION_DIRT_COUNT,
                                    EXPLOSION_DIRT_RADIUS, EXPLOSION_DIRT_TTL))
            {
                tankList->projectile_list->Add(shrapnel);
            }
            /* Finally, remove it: */
            this->Invalidate();
            return;
        }
    }
}

void Bullet::Draw(DrawBuffer * drawBuffer)
{
    drawBuffer->SetPixel(Position{this->pos.x, this->pos.y}, Palette.Get(Colors::FireHot));
    drawBuffer->SetPixel(Position{this->pos_blur_from.x, this->pos_blur_from.y}, Palette.Get(Colors::FireCold));
}

void Bullet::Erase(DrawBuffer * drawBuffer, Level *)
{
    level->CommitPixel(Position{this->pos.x, this->pos.y});
    level->CommitPixel(Position{this->pos_blur_from.x, this->pos_blur_from.y});
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
    Position adjusted_pos = {this->pos.x / 16, this->pos.y / 16};

    /* Make sure we didn't hit a level detail: */
    LevelVoxel c = level->GetVoxel(adjusted_pos);
    if (Voxels::IsBlockingCollision(c))
    {
        this->Invalidate();
        return;
    }

    /* Effects blank everything out in their paths: */
    level->SetVoxel(adjusted_pos, Random.Bool(500) ? LevelVoxel::DecalHigh : LevelVoxel::DecalLow);
}

void Shrapnel::Draw(DrawBuffer * drawBuffer)
{
    drawBuffer->SetPixel(Position{this->pos.x / 16, this->pos.y / 16}, Palette.Get(Colors::FireHot));
}

void Shrapnel::Erase(DrawBuffer * drawBuffer, Level *)
{
    level->CommitPixel(Position{this->pos.x / 16, this->pos.y / 16});
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
            Shrapnel{Position{pos.x * 16 + 8, pos.y * 16 + 8},
                       SpeedF{float(Random.Int(0, radius) - radius / 2), float(Random.Int(0, radius) - radius / 2)},
                       Random.Int(0, ttl), level});
    }
    return items;
}
