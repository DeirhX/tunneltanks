#include "projectiles.h"
#include <boost/circular_buffer.hpp>
#include <Terrain.h>
#include <random.h>
#include <tank.h>
#include <tweak.h>

#include "world.h"
#include "mymath.h"
#include "raycaster.h"
#include "tank_list.h"

void Bullet::Advance(TankList *)
{
    auto IteratePositions = [this](PositionF tested_pos, PositionF prev_pos) {
        this->pos = tested_pos;
        this->pos_blur_from = prev_pos;

        if (GetWorld()->GetCollisionSolver()->TestCollide(this->pos.ToIntPosition(), 
              [this](Tank & tank)
        {
                if (tank.GetColor() == this->tank->GetColor())
                    return false;
                tank.GetReactor().Exhaust(tweak::tank::ShotDamage);
                return true;
        },
        [](Machine & machine)
        {
            if (!machine.IsBlockingCollision())
                return false;
            machine.GetReactor().Exhaust(tweak::tank::ShotDamage);
            return true;
        },
        [](TerrainPixel level_pixel) { return Pixel::IsAnyCollision(level_pixel); }))

        {
            GetWorld()->GetProjectileList()->Add(ExplosionDesc::AllDirections(
                                           this->pos_blur_from.ToIntPosition(), tweak::explosion::normal::ShrapnelCount,
                                           tweak::explosion::normal::Speed, tweak::explosion::normal::Frames)
                                           .Explode<Shrapnel>(*level));
            /* Finally, remove it: */
            this->Invalidate();
            return false;
        };

        return true;
    };
    Raycaster::Cast(this->pos, this->pos + (this->speed), IteratePositions);
}

void Bullet::Draw(Surface * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::FireHot));
    drawBuffer->SetPixel(this->pos_blur_from.ToIntPosition(), Palette.Get(Colors::FireCold));
}

template <typename ExplosionFuncType>
void FlyingBarrel::Advance(TankList *, ExplosionFuncType explosionFunc)
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

    auto IteratePositions = [this, &search_step, &prev_positions](PositionF tested_pos, PositionF) {
        prev_positions.push_back(tested_pos);
        ++search_step;

        bool is_collision = GetWorld()->GetCollisionSolver()->TestCollide(
            tested_pos.ToIntPosition(),
            [this](Tank & tank) { return tank.GetColor() != this->tank->GetColor(); },
            [](Machine &) { return true; },
            [](TerrainPixel & pixel) { return Pixel::IsAnyCollision(pixel); });

        if (is_collision)
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
        explosionFunc(this->pos, this->speed, this->level, GetWorld()->GetProjectileList());
    }
}

void FlyingBarrel::Draw(Surface * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), draw_color);
}

void ConcreteBarrel::Advance(TankList * tankList)
{
    auto ExplosionFunc = [](PositionF proj_position, SpeedF proj_speed, Terrain * proj_level, ProjectileList * projectile_list) {
        projectile_list->Add(ExplosionDesc::Fan(proj_position.ToIntPosition(), proj_speed, math::Radians{math::half_pi},
                                                tweak::explosion::dirt::ShrapnelCount, tweak::explosion::dirt::Speed,
                                                tweak::explosion::dirt::Frames)
                                 .Explode<ConcreteFoam>(*proj_level));
    };
    FlyingBarrel::Advance(tankList, ExplosionFunc);
}

void DirtBarrel::Advance(TankList * tankList)
{
    auto ExplosionFunc = [](PositionF proj_position, SpeedF proj_speed, Terrain * proj_level, ProjectileList * projectile_list) {
        projectile_list->Add(ExplosionDesc::Fan(proj_position.ToIntPosition(), proj_speed, math::Radians{math::half_pi},
                                                tweak::explosion::dirt::ShrapnelCount, tweak::explosion::dirt::Speed,
                                                tweak::explosion::dirt::Frames)
                                 .Explode<DirtFoam>(*proj_level));
    };
    FlyingBarrel::Advance(tankList, ExplosionFunc);
}

/* Le Shrapnel */

void ShrapnelBase::Advance(TankList * tankList)
{
    auto AdvanceStepFunc = [this](PositionF, PositionF, TankList *) {
        /* Make sure we didn't hit a level detail: */
        TerrainPixel c = level->GetPixel(this->pos.ToIntPosition());
        if (Pixel::IsBlockingCollision(c))
        {
            if ((Pixel::IsConcrete(c) && Random.Bool(tweak::explosion::ChanceToDestroyConcrete))
                || (Pixel::IsRock(c) && Random.Bool(tweak::explosion::ChanceToDestroyRock)))
            {
                level->SetPixel(this->pos.ToIntPosition(),
                                Random.Bool(500) ? TerrainPixel::DecalHigh : TerrainPixel::DecalLow);
            }
            this->Invalidate();
            return false;
        }
        /* Effects blank everything out in their paths: */
        level->SetPixel(this->pos.ToIntPosition(), Random.Bool(500) ? TerrainPixel::DecalHigh : TerrainPixel::DecalLow);
        return true;
    };

    AdvanceShrapnel(tankList, AdvanceStepFunc);
}

template <typename OnAdvanceFuncType>
void ShrapnelBase::AdvanceShrapnel(TankList * tankList, OnAdvanceFuncType OnAdvanceFunc)
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


void ShrapnelBase::Draw(Surface * drawBuffer)
{
    drawBuffer->SetPixel(this->pos.ToIntPosition(), Palette.Get(Colors::FireHot));
}

void ConcreteFoam::Advance(TankList * tankList)
{
    auto AdvanceStepFunc = [this](PositionF, PositionF prev_pos, TankList *) {

        /* Make sure we didn't hit a level detail: */
        TerrainPixel c = level->GetPixel(this->pos.ToIntPosition());
        if (Pixel::IsAnyCollision(c) && !Pixel::IsConcrete(c))
        {
            level->SetPixel(prev_pos.ToIntPosition(), Random.Bool(500) ? TerrainPixel::ConcreteHigh : TerrainPixel::ConcreteLow);
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
    auto AdvanceStepFunc = [this](PositionF, PositionF prev_pos, TankList *) {
        /* Make sure we didn't hit a level detail: */
        TerrainPixel c = level->GetPixel(this->pos.ToIntPosition());
        if (Pixel::IsAnyCollision(c))
        {
            level->SetPixel(prev_pos.ToIntPosition(),
                            Random.Bool(500) ? TerrainPixel::DirtHigh : TerrainPixel::DirtLow);
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
