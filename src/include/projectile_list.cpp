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
    this->bullets.Shrink();
    this->shrapnels.Shrink();
}

void ProjectileList::Advance(Level * level, TankList * tankList)
{
    auto newly_added = std::vector<Shrapnel>{};

    for (Shrapnel & p : this->shrapnels)
    {
        /* Did this expire? */
        if (!p.steps_remain)
        {
            Remove(p);
            continue;
        }

        /* Move the effect: */
        p.steps_remain--;
        p.pos.x += p.speed.x;
        p.pos.y += p.speed.y;
        Position pos = {p.pos.x / 16, p.pos.y / 16};

        /* Make sure we didn't hit a level detail: */
        LevelVoxel c = level->GetVoxel(pos);
        if (Voxels::IsBlockingCollision(c))
        {
            Remove(p);
            continue;
        }

        /* Effects blank everything out in their paths: */
        level->SetVoxel(pos, Random.Bool(500) ? LevelVoxel::DecalHigh : LevelVoxel::DecalLow);
    }

    /* Bullet: */
    for (Bullet & p : this->bullets)
    {
        for (int i = 0; i < p.steps_remain; i++)
        {
            p.pos_blur_from.x = p.pos.x;
            p.pos_blur_from.y = p.pos.y;
            p.pos.x += p.speed.x;
            p.pos.y += p.speed.y;

            /* Did we hit another tank? */
            int hitTankColor = p.tank->GetColor();
            Tank * hitTank = tankList->GetTankAtPoint(p.pos.x, p.pos.y, hitTankColor);
            if (hitTank)
            {
                /* If we have an associated tank, return the shot: */
                p.tank->ReturnBullet();

                /* Hurt the tank we hit: */
                hitTank->AlterHealth(tweak::tank::ShotDamage);

                /* Add all of the effect particles: */

                newly_added.reserve(newly_added.size() + EXPLOSION_HURT_COUNT);
                for (Shrapnel & shrapnel :
                     Explosion::Explode(Position{p.pos_blur_from.x, p.pos_blur_from.y}, level,
                                                 EXPLOSION_HURT_COUNT,
                                                 EXPLOSION_HURT_RADIUS, EXPLOSION_HURT_TTL))
                {
                    newly_added.emplace_back(shrapnel);
                }

                /* Finally, remove it: */
                Remove(p);
                break;
            }

            /* Else, did we hit something in the level? */
            LevelVoxel c = level->GetVoxel(p.pos);
            if (Voxels::IsAnyCollision(c))
            {
                /* If we have an associated tank, return the shot: */
                p.tank->ReturnBullet();

                /* Add all of the effect particles: */
                newly_added.reserve(newly_added.size() + EXPLOSION_DIRT_COUNT);
                for (Shrapnel & shrapnel :
                     Explosion::Explode(Position{p.pos_blur_from.x, p.pos_blur_from.y}, level,
                                                 EXPLOSION_DIRT_COUNT,
                                                 EXPLOSION_DIRT_RADIUS, EXPLOSION_DIRT_TTL))
                {
                    newly_added.emplace_back(shrapnel);
                }

                /* Finally, remove it: */
                Remove(p);
                break;
            }
        }
    }

    for (Shrapnel& new_spawn: newly_added)
	    Add(new_spawn);
	Shrink();
}

void ProjectileList::Erase(DrawBuffer* drawBuffer, Level* level)
{
    for (Shrapnel & shrapnel : this->shrapnels)
    {
        level->CommitPixel(Position{shrapnel.pos.x / 16, shrapnel.pos.y / 16});
    }
    for (Bullet & bullet : this->bullets)
    {
        level->CommitPixel(Position{bullet.pos.x, bullet.pos.y});
        level->CommitPixel(Position{bullet.pos_blur_from.x, bullet.pos_blur_from.y});
    }
}


void ProjectileList::Draw(DrawBuffer* drawBuffer)
{
    for (Shrapnel & shrapnel : this->shrapnels)
    {
        drawBuffer->SetPixel(Position{shrapnel.pos.x / 16, shrapnel.pos.y / 16}, Palette.Get(Colors::FireHot));
    }
    for (Bullet & bullet : this->bullets)
    {
        drawBuffer->SetPixel(Position{bullet.pos.x, bullet.pos.y}, Palette.Get(Colors::FireHot));
        drawBuffer->SetPixel(Position{bullet.pos_blur_from.x, bullet.pos_blur_from.y}, Palette.Get(Colors::FireCold));
    }
}
