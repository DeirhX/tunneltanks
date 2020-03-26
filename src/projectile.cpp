#include "base.h"
#include <cstdio>
#include <boost/circular_buffer.hpp>
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
                 Explosion::Explode(this->pos_blur_from.ToIntPosition(), level, tweak::explosion::normal::ShrapnelCount,
                                    tweak::explosion::normal::Speed, tweak::explosion::normal::Frames))
            {
                tankList->projectile_list->Add(shrapnel);
            }
            /* Finally, remove it: */
            this->Invalidate();
            return false;
        }

        /* Else, did we hit something in the level? */
        LevelPixel c = level->GetVoxel(this->pos.ToIntPosition());
        if (Pixel::IsAnyCollision(c))
        {
            /* If we have an associated tank, return the shot: */
            this->tank->ReturnBullet();

            for (Shrapnel & shrapnel :
                 Explosion::Explode(this->pos_blur_from.ToIntPosition(), level, tweak::explosion::dirt::ShrapnelCount,
                                    tweak::explosion::dirt::Speed, tweak::explosion::dirt::Frames))
            {
                tankList->projectile_list->Add(shrapnel);
            }
            /* Finally, remove it: */
            this->Invalidate();
            return false;
        }

        return true;
    };
    Raycaster::Cast(this->pos, this->pos + (this->direction * float(this->simulation_steps)), IteratePositions);
}

void Bullet::Draw(LevelDrawBuffer * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::FireHot));
    drawBuffer->SetPixel(this->pos_blur_from.ToIntPosition(), Palette.Get(Colors::FireCold));
}

void Bullet::Erase(LevelDrawBuffer * drawBuffer, Level *)
{
    level->CommitPixel(this->pos.ToIntPosition());
    level->CommitPixel(this->pos_blur_from.ToIntPosition());
}


/* Le Spray de la Concrete */

void ConcreteSpray::Advance(TankList * tankList)
{
    /* Projectile exploding {explode_dist} pixels before contact.
     *
     * Maintain cache of [explode_dist] positions before the current one
     *  We will test always [explode_dist] amount of positions in front of the projectile trajectory.
     *  If there is a hypothetical collision, we attempt to explode {explode_dist} pixels away.
     */
    constexpr int explode_dist = 3;
    auto prev_positions = boost::circular_buffer<PositionF>{explode_dist};
    int search_step = 0;
    const int search_step_count = explode_dist + int(flight_speed);
    PositionF advanced_pos = {};

    auto IteratePositions = [this, &search_step, &prev_positions, tankList](PositionF tested_pos,
                                                                                          PositionF prev_pos) {
        prev_positions.push_back(tested_pos);
        ++search_step;

        Tank * hitTank = tankList->GetTankAtPoint(tested_pos.ToIntPosition(), this->tank->GetColor());
        if (hitTank)
        {
            this->Invalidate();
            return false;
        }
        LevelPixel c = level->GetVoxel(tested_pos.ToIntPosition());
        if (Pixel::IsAnyCollision(c))
        {
            this->Invalidate();
            return false;
        }
        return true;
    };
    bool collided = !Raycaster::Cast(this->pos, this->pos + (this->direction * float(search_step_count)), IteratePositions, Raycaster::VisitFlags::PixelsMustTouchCorners);

    /* Now divine a position {explode_dist} steps past in the simulation and explode there if needed
     * It will always be in the first slot of the circular buffer
     */
    this->pos = prev_positions[0];

    if (collided)
    {
        for (Shrapnel & shrapnel :
             Explosion::Explode(this->pos.ToIntPosition(), level, tweak::explosion::dirt::ShrapnelCount,
                                tweak::explosion::dirt::Speed, tweak::explosion::dirt::Frames))
        {
            tankList->projectile_list->Add(shrapnel);
        }
    }
}

void ConcreteSpray::Draw(LevelDrawBuffer * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::ConcreteShot));
}

void ConcreteSpray::Erase(LevelDrawBuffer * drawBuffer, Level * )
{
    level->CommitPixel(this->pos.ToIntPosition());
}

/* Le Shrapnel */

void Shrapnel::Advance(TankList * tankList)
{
    /* Did this expire? */
    if (!this->life)
    {
        this->Invalidate();
        return;
    }

    /* Move the effect: */
    this->life--;
    this->pos += this->direction;

    /* Make sure we didn't hit a level detail: */
    LevelPixel c = level->GetVoxel(this->pos.ToIntPosition());
    if (Pixel::IsBlockingCollision(c))
    {
        this->Invalidate();
        return;
    }

    /* Effects blank everything out in their paths: */
    level->SetVoxel(this->pos.ToIntPosition(), Random.Bool(500) ? LevelPixel::DecalHigh : LevelPixel::DecalLow);
}

void Shrapnel::Draw(LevelDrawBuffer * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::FireHot));
}

void Shrapnel::Erase(LevelDrawBuffer * drawBuffer, Level *)
{
    level->CommitPixel(this->pos.ToIntPosition());
}

std::vector<Shrapnel> Explosion::Explode(Position pos, Level *level, int count, int speed, int ttl)
{
    auto items = std::vector<Shrapnel>{};
    items.reserve(count);
    /* Add all of the effect particles: */
    for (int i = 0; i < count; i++)
    {
        items.emplace_back(
            Shrapnel{Position{pos.x, pos.y},
                     SpeedF{Random.Float(-speed / 32.f, speed / 32.f), Random.Float(-speed / 32.f, speed / 32.f)},
                       Random.Int(0, ttl), level});
    }
    return items;
}

std::vector<Shrapnel> Explosion::FanOut(Position pos, DirectionF direction, Level * level, int count, int radius, int ttl)
{
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
