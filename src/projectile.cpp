#include "projectile.h"
#include "base.h"
#include <boost/circular_buffer.hpp>
#include <cstdio>
#include <level.h>
#include <random.h>
#include <tank.h>
#include <tweak.h>

#include "mymath.h"
#include "raycaster.h"
#include "tanklist.h"

void Bullet::Advance(TankList * tankList)
{
    auto IteratePositions = [this, tankList](PositionF tested_pos, PositionF prev_pos) {
        this->pos = tested_pos;
        this->pos_blur_from = prev_pos;

        /* Did we hit another tank? */
        TankColor hitTankColor = this->tank->GetColor();
        Tank * hitTank = tankList->GetTankAtPoint(this->pos.ToIntPosition(), hitTankColor);
        if (hitTank)
        {
            /* Hurt the tank we hit: */
            hitTank->AlterHealth(tweak::tank::ShotDamage);

            /* Add all of the effect particles: */

            for (Shrapnel & shrapnel : ExplosionDesc::AllDirections(
                                           this->pos_blur_from.ToIntPosition(), tweak::explosion::normal::ShrapnelCount,
                                           tweak::explosion::normal::Speed, tweak::explosion::normal::Frames)
                                           .Explode<Shrapnel>(level))
            {
                tankList->projectile_list->Add(shrapnel);
            }
            /* Finally, remove it: */
            this->Invalidate();
            return false;
        }

        /* Else, did we hit something in the level? */
        LevelPixel c = level->GetPixel(this->pos.ToIntPosition());
        if (Pixel::IsAnyCollision(c))
        {
            for (Shrapnel & shrapnel : ExplosionDesc::AllDirections(
                                           this->pos_blur_from.ToIntPosition(), tweak::explosion::dirt::ShrapnelCount,
                                           tweak::explosion::dirt::Speed, tweak::explosion::dirt::Frames)
                                           .Explode<Shrapnel>(level))
            {
                tankList->projectile_list->Add(shrapnel);
            }
            /* Finally, remove it: */
            this->Invalidate();
            return false;
        }

        return true;
    };
    Raycaster::Cast(this->pos, this->pos + (this->speed), IteratePositions);
}

void Bullet::Draw(Surface * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::FireHot));
    drawBuffer->SetPixel(this->pos_blur_from.ToIntPosition(), Palette.Get(Colors::FireCold));
}

void Bullet::Erase(Surface * drawBuffer, Level *)
{
    level->CommitPixel(this->pos.ToIntPosition());
    level->CommitPixel(this->pos_blur_from.ToIntPosition());
}

template <typename ExplosionFuncType>
void FlyingBarrel::Advance(TankList * tankList, ExplosionFuncType explosionFunc)
{
    /* Projectile exploding {explode_dist} pixels before contact.
     *
     * Maintain cache of [explode_dist] positions before the current one
     *  We will test always [explode_dist] amount of positions in front of the projectile trajectory.
     *  If there is a hypothetical collision, we attempt to explode {explode_dist} pixels away.
     */
    DirectionF direction = this->speed.Normalize();
    auto prev_positions = boost::circular_buffer<PositionF>{this->explode_distance + 1ull};
    int search_step = 0;
    const int search_step_count = this->explode_distance + int(std::round(this->speed.GetSize()));
    
    PositionF advanced_pos = {};

    auto IteratePositions = [this, &search_step, &prev_positions, tankList](PositionF tested_pos, PositionF prev_pos) {
        prev_positions.push_back(tested_pos);
        ++search_step;

        Tank * hitTank = tankList->GetTankAtPoint(tested_pos.ToIntPosition(), this->tank->GetColor());
        if (hitTank)
        {
            this->Invalidate();
            return false;
        }
        LevelPixel c = level->GetPixel(tested_pos.ToIntPosition());
        if (Pixel::IsAnyCollision(c))
        {
            this->Invalidate();
            return false;
        }
        return true;
    };
    bool collided = !Raycaster::Cast(this->pos, this->pos + (direction * float(search_step_count)),
                                     IteratePositions, Raycaster::VisitFlags::PixelsMustTouchCorners);

    /* Now divine a position {explode_dist} steps past in the simulation and explode there if needed
     * It will always be in the first slot of the circular buffer
     */
    this->pos = prev_positions[0];

    if (collided)
    {
        explosionFunc(this->pos, this->speed, this->level, tankList->projectile_list);
    }
}

void FlyingBarrel::Draw(Surface * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), draw_color);
}

void FlyingBarrel::Erase(Surface * drawBuffer, Level *)
{
    level->CommitPixel(this->pos.ToIntPosition());
}

void ConcreteBarrel::Advance(TankList * tankList)
{
    auto ExplosionFunc = [](PositionF proj_position, SpeedF proj_speed, Level * proj_level, ProjectileList * projectile_list) {
        for (auto & shrapnel : ExplosionDesc::Fan(proj_position.ToIntPosition(), proj_speed, math::Radians{math::half_pi},
                                                  tweak::explosion::dirt::ShrapnelCount, tweak::explosion::dirt::Speed,
                                                  tweak::explosion::dirt::Frames)
                                   .Explode<ConcreteFoam>(proj_level))
        {
            projectile_list->Add(shrapnel);
        }
    };
    FlyingBarrel::Advance(tankList, ExplosionFunc);
}

void DirtBarrel::Advance(TankList * tankList)
{
    auto ExplosionFunc = [](PositionF proj_position, SpeedF proj_speed, Level * proj_level, ProjectileList * projectile_list) {
        for (auto & shrapnel : ExplosionDesc::Fan(proj_position.ToIntPosition(), proj_speed, math::Radians{math::half_pi},
                                                  tweak::explosion::dirt::ShrapnelCount, tweak::explosion::dirt::Speed,
                                                  tweak::explosion::dirt::Frames)
                                   .Explode<DirtFoam>(proj_level))
        {
            projectile_list->Add(shrapnel);
        }
    };
    FlyingBarrel::Advance(tankList, ExplosionFunc);
}

/* Le Shrapnel */

void Shrapnel::Advance(TankList * tankList)
{
    auto AdvanceStepFunc = [this](PositionF tested_pos, PositionF prev_pos, TankList * tankList) {
        /* Make sure we didn't hit a level detail: */
        LevelPixel c = level->GetPixel(this->pos.ToIntPosition());
        if (Pixel::IsBlockingCollision(c))
        {
            if ((Pixel::IsConcrete(c) && Random.Bool(tweak::explosion::ChanceToDestroyConcrete))
                || (Pixel::IsRock(c) && Random.Bool(tweak::explosion::ChanceToDestroyRock)))
            {
                level->SetPixel(this->pos.ToIntPosition(),
                                Random.Bool(500) ? LevelPixel::DecalHigh : LevelPixel::DecalLow);
            }
            this->Invalidate();
            return false;
        }
        /* Effects blank everything out in their paths: */
        level->SetPixel(this->pos.ToIntPosition(), Random.Bool(500) ? LevelPixel::DecalHigh : LevelPixel::DecalLow);
        return true;
    };

    AdvanceShrapnel(tankList, AdvanceStepFunc);
}

template <typename OnAdvanceFuncType>
void Shrapnel::AdvanceShrapnel(TankList * tankList, OnAdvanceFuncType OnAdvanceFunc)
{
    /* Did this expire? */
    if (!this->life--)
    {
        this->Invalidate();
        return;
    }

    auto IteratePositions = [this, OnAdvanceFunc, tankList](PositionF tested_pos, PositionF prev_pos) {
        /* Move the effect: */
        this->pos = tested_pos;

        return OnAdvanceFunc(tested_pos, prev_pos, tankList);
    };

    Raycaster::Cast(this->pos, this->pos + this->speed, IteratePositions,
                    Raycaster::VisitFlags::PixelsMustTouchCorners);
}


void Shrapnel::Draw(Surface * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::FireHot));
}

void Shrapnel::Erase(Surface * drawBuffer, Level *) { level->CommitPixel(this->pos.ToIntPosition()); }

void ConcreteFoam::Advance(TankList * tankList)
{
    auto AdvanceStepFunc = [this](PositionF tested_pos, PositionF prev_pos, TankList * tankList) {

        /* Make sure we didn't hit a level detail: */
        LevelPixel c = level->GetPixel(this->pos.ToIntPosition());
        if (Pixel::IsAnyCollision(c) && !Pixel::IsConcrete(c))
        {
            level->SetPixel(prev_pos.ToIntPosition(), Random.Bool(500) ? LevelPixel::ConcreteHigh : LevelPixel::ConcreteLow);
            this->Invalidate();
            return false;
        }
        return true;
    };

    AdvanceShrapnel(tankList, AdvanceStepFunc);
}

void ConcreteFoam::Draw(Surface * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::ConcreteShot));
}

void DirtFoam::Advance(TankList * tankList)
{
    auto AdvanceStepFunc = [this](PositionF tested_pos, PositionF prev_pos, TankList * tankList) {
        /* Make sure we didn't hit a level detail: */
        LevelPixel c = level->GetPixel(this->pos.ToIntPosition());
        if (Pixel::IsAnyCollision(c))
        {
            level->SetPixel(prev_pos.ToIntPosition(),
                            Random.Bool(500) ? LevelPixel::DirtHigh : LevelPixel::DirtLow);
            this->Invalidate();
            return false;
        }
        return true;
    };

    AdvanceShrapnel(tankList, AdvanceStepFunc);
}

void DirtFoam::Draw(Surface * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::DirtContainerShot));
}
