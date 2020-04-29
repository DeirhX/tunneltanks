#include "tank.h"
#include "algorithm"
#include "controller.h"
#include "level.h"
#include "level_view.h"
#include "projectile.h"
#include "random.h"
#include "screen.h"
#include "tank_base.h"
#include "tank_list.h"
#include "tank_sprites.h"
#include "tweak.h"
#include "world.h"

/*  /\
 * TANK
 */

PublicTankInfo::PublicTankInfo(const Controllable & controllable, Position position_relative_to)
    : health(controllable.GetHealth()), energy(controllable.GetEnergy()),
      relative_pos{static_cast<int>(controllable.GetPosition().x - position_relative_to.x),
                   static_cast<int>(controllable.GetPosition().y - position_relative_to.y)},
      level_view(LevelView{&controllable, controllable.GetLevel()})
{
}

Tank::Tank(TankColor color, Level * level, TankBase * tank_base)
    : Base(tank_base->GetPosition(), tweak::tank::DefaultTankReactor, tweak::tank::ResourcesMax, level), color(color), tank_base(tank_base),
      turret(this, Palette.GetTank(color)[2]), materializer(this, &this->GetResources())
{
    // this->cached_slice = std::make_shared<LevelView>(this, lvl);

    /* Let's just make the starting direction random, because we can: */
    auto dir = Direction{Random.Int(0, 7)};
    if (dir >= 4)
        dir.Get()++;
    this->direction = dir; // DirectionF{ dir };

    Spawn();
}

void Tank::SetCrosshair(widgets::Crosshair * cross)
{
    this->crosshair = cross;
    this->crosshair->SetWorldPosition(this->GetPosition() + Offset{0, -10});
}

/* The advance step, once per frame. Do everything. */
void Tank::Advance(World & world)
{
    if (!this->HealthOrEnergyEmpty())
    {
        /* Recharge and discharge */
        this->GetReactor().Exhaust(tweak::tank::IdleCost);

        TankBase * base = this->level->CheckBaseCollision(this->position);
        if (base)
        {
            this->TryBaseHeal(*base);
            this->TransferResourcesToBase(*base);
        }

        /* Get input from controller and figure what we *want* to do and do it: rotate turret, set desired direction and speed  */
        if (this->controller)
        {
            Position spawn_pos = this->level->GetSpawn(this->color)->GetPosition();
            auto controls = PublicTankInfo{*this, spawn_pos};
            this->ApplyControllerOutput(this->controller->ApplyControls(controls));
        }

        /* Rotate the turret temporarily back directly to match our heading direction */
        if (this->speed.x || this->speed.y)
        {
            this->turret.SetDirection(DirectionF{Direction::FromSpeed(this->speed)});
        }
        /* Now override this by rotating according to crosshair if it is being used */
        this->turret.Advance(this->GetPosition(), this->crosshair);
        this->materializer.Advance(this->GetPosition());

        /* Move, dig and solve collisions with other tanks */
        if (this->HandleMove(this->GetDirection(), this->turret.IsShooting()))
        {   /* Well, we moved, so let's charge ourselves: */
            this->GetReactor().Exhaust(tweak::tank::MoveCost);
        }
        this->link_source.UpdatePosition(this->GetPosition());
        this->CollectItems();

        /* Shoot the turret if desired */
        this->turret.HandleShoot();
    }
    else
    {
        AdvanceDeath(world);
    }
}

void Tank::AdvanceDeath(World & world)
{
    /* DEaD. Handle respawning. */
    if (!this->respawn_timer.IsRunning())
    {
        Die();
    }

    if (this->respawn_timer.AdvanceAndCheckElapsed())
    {
        if (--this->lives_left)
        {
            Spawn();
        }
        else
        {
            bool players_remaining =
                std::any_of(world.GetTankList()->begin(), world.GetTankList()->end(),
                            [](Tank & tank) { return tank.controller->IsPlayer() && !tank.HealthOrEnergyEmpty(); });
            if (!players_remaining)
                world.SetGameOver();
        }
    }
}

/* We don't use the Tank structure in this function, since we are checking the
 * tank's hypothetical position... ie: IF we were here, would we collide? */
CollisionType Tank::TryCollide(Direction rotation, Position position_)
{
    CollisionType result = CollisionType::None;

    tank::ForEachTankPixel(
        [this, &result](Position position_) {
            bool is_blocking_collision = GetWorld()->GetCollisionSolver()->TestCollide(
                position_,
                [this, &result](Tank & tank) {
                    if (tank.GetColor() != this->GetColor())
                    {
                        result = CollisionType::Blocked;
                        return true;
                    }
                    return false;
                }, 
                [&result](auto & machine) {
                    /* Collisions with machines disabled */
                    return false;
                    if (machine.IsBlockingCollision())
                    {
                        result = CollisionType::Blocked;
                        return true;
                    }
                    return false;
                },
                [&result](LevelPixel & pixel) {
                    if (Pixel::IsDirt(pixel))
                        result = CollisionType::Dirt;

                    if (Pixel::IsBlockingCollision(pixel))
                    {
                        result = CollisionType::Blocked;
                        return true;
                    }
                    return false;
                });

            return !is_blocking_collision;
    }, position_, rotation);

    //if (result == CollisionType::Blocked || GetWorld()->GetTankList()->CheckForCollision(*this, position, rotation))
    //    return CollisionType::Blocked;

    return result;
}


/* Check to see if we're in any bases, and heal based on that: */
void Tank::TryBaseHeal(TankBase & base) { base.RechargeTank(this); }

void Tank::TransferResourcesToBase(TankBase & base)
{
    if (base.GetColor() == this->GetColor())
    {
        base.AbsorbResources(this->resources, tweak::base::MaterialsAbsorbRate);
    }
}

void Tank::CollectItems()
{
    this->ForEachTankPixel([this](Position world_position) {
        LevelPixel pixel = this->level->GetPixel(world_position);
        if (Pixel::IsEnergy(pixel))
        {
            this->level->SetPixel(world_position, LevelPixel::Blank);
            EnergyAmount energy_collected = 100_energy;
            if (pixel == LevelPixel::EnergyMedium)
                energy_collected.amount *= 2;
            else if (pixel == LevelPixel::EnergyHigh)
                energy_collected.amount *= 4;
            this->GetReactor().Add(energy_collected);
        }
        return true;
    });
}

void Tank::Draw(Surface & surface) const
{
    if (!this->reactor.GetHealth())
        return;

    for (int y = 0; y < 7; y++)
        for (int x = 0; x < 7; x++)
        {
            char val = TANK_SPRITE[this->direction][y][x];
            if (val)
                surface.SetPixel(Position{this->position.x + x - 3, this->position.y + y - 3},
                                   Palette.GetTank(this->color)[val - 1]);
        }

    this->turret.Draw(&surface);
}

void Tank::Spawn()
{
    this->turret.Reset();
    this->reactor.Fill();
    this->position = this->tank_base->GetPosition();
    this->link_source.Enable();
}

void Tank::Die()
{
    /* Begin respawn timer and trigger a nice explosion */
    this->reactor.Clear();
    this->resources.Clear();
    this->respawn_timer.Restart();
    this->link_source.Disable();

    GetWorld()->GetProjectileList()->Add(
        ExplosionDesc::AllDirections(this->position, tweak::explosion::death::ShrapnelCount,
                                     tweak::explosion::death::Speed, tweak::explosion::death::Frames)
            .Explode<Shrapnel>(this->level));
}

void Tank::ApplyControllerOutput(ControllerOutput controls)
{
    this->turret.ApplyControllerOutput(controls);
    this->materializer.ApplyControllerOutput(controls);
    this->speed = controls.speed;
    if (this->crosshair)
    {
        if (controls.is_crosshair_absolute)
        {
            this->crosshair->SetScreenRelativePosition(controls.crosshair_screen_pos);
        }
        else
        {
            this->crosshair->SetRelativeDirection(this, controls.crosshair_direction);
        }
    }
}
