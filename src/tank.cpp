#include "base.h"
#include <algorithm>

#include <level.h>
#include <level_view.h>
#include <projectile.h>
#include <random.h>
#include <screen.h>
#include <tank.h>
#include <tank_base.h>
#include <tank_list.h>
#include <tank_sprites.h>
#include <tweak.h>
#include <world.h>


#include "controller.h"
#include "game.h"
#include "raycaster.h"

bool Resources::PayDirt(int amount)
{
    if (dirt - amount < 0)
        return false;
    dirt -= amount;
    return true;
} /* Handle shooting buttons and weapon changes */

bool Resources::PayMinerals(int amount)
{
    if (this->minerals - amount < 0)
        return false;
    this->minerals -= amount;
    return true;
}

bool Resources::Pay(Cost payment)
{
    if (this->dirt - payment.dirt < 0 || this->minerals - payment.minerals < 0)
        return false;
    this->dirt -= payment.dirt;
    this->minerals -= payment.minerals;
    return true;
}


/*  /\
 * TANK
 */

Tank::Tank(TankColor color, Level * level, ProjectileList * projectile_list, TankBase * tank_base)
    : is_valid(true), pos(tank_base->GetPosition()), color(color), tank_base(tank_base),
      turret(this, Palette.GetTank(color)[2]), materializer(this, &this->resources)
{
    // this->cached_slice = std::make_shared<LevelView>(this, lvl);

    /* Let's just make the starting direction random, because we can: */
    auto dir = Direction{Random.Int(0, 7)};
    if (dir >= 4)
        dir.Get()++;

    this->direction = dir; // DirectionF{ dir };
    this->level = level;
    this->projectile_list = projectile_list;
}

void Tank::SetCrosshair(widgets::Crosshair * cross)
{
    this->crosshair = cross;
    this->crosshair->SetWorldPosition(this->GetPosition() + Offset{0, -10});
}

/* The advance step, once per frame. Do everything. */
void Tank::Advance(World * world)
{
    if (!this->IsDead())
    {
        /* Recharge and discharge */
        this->AlterEnergy(tweak::tank::IdleCost);
        this->TryBaseHeal();

        /* Get input from controller and figure what we *want* to do and do it: rotate turret, set desired direction and speed  */
        if (this->controller)
        {
            Vector base = this->level->GetSpawn(this->color)->GetPosition();
            PublicTankInfo controls = {.health = this->health,
                                       .energy = this->energy,
                                       .x = static_cast<int>(this->pos.x - base.x),
                                       .y = static_cast<int>(this->pos.y - base.y),
                                       .level_view = LevelView(this, this->level)};
            this->ApplyControllerOutput(this->controller->ApplyControls(&controls));
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
        this->HandleMove(world->GetTankList());

        this->CollectItems();

        /* Shoot the turret if desired */
        this->turret.HandleShoot();
    }
    else
    {
        /* DEaD. Handle respawning. */
        if (this->respawn_timer.AdvanceAndCheckElapsed())
        {
            if (--this->lives_left)
            {
                Spawn();
            }
            else
            {
                bool players_remaining =
                    std::any_of(world->GetTankList()->begin(), world->GetTankList()->end(),
                                [](Tank & tank) { return tank.controller->IsPlayer() && !tank.IsDead(); });
                if (!players_remaining)
                    world->GameIsOver();
            }
        }
    }
}

/* We don't use the Tank structure in this function, since we are checking the
 * tank's hypothetical position... ie: IF we were here, would we collide? */
CollisionType Tank::GetCollision(Direction dir, Position position, TankList * tank_list)
{
    CollisionType result = CollisionType::None;

    tank::ForEachTankPixel(
        [this, &result](Position position) {
            bool is_blocking_collision = GetWorld()->GetCollisionSolver()->TestCollide(
                position,
                [this, &result](Tank & tank) {
                    if (tank.GetColor() != this->GetColor())
                    {
                        result = CollisionType::Blocked;
                        return true;
                    }
                    return false;
                },
                [&result](Machine & machine) {
                    result = CollisionType::Blocked;
                    return true;
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
    }, position, dir);

    if (result == CollisionType::Blocked || tank_list->CheckForCollision(*this, position, dir))
        return CollisionType::Blocked;

    return result;
}

void Tank::HandleMove(TankList * tl)
{
    /* Calculate the direction: */
    if (this->speed.x != 0 || this->speed.y != 0)
    {
        Direction dir = Direction::FromSpeed(this->speed);
        CollisionType collision = this->GetCollision(dir, this->pos + 1 * this->speed, tl);
        /* Now, is there room to move forward in that direction? */
        if (collision != CollisionType::None)
        {
            /* Attempt to dig and see the results */
            DigResult dug = this->level->DigTankTunnel(this->pos + (1 * this->speed), this->turret.IsShooting());
            this->resources.AddDirt(dug.dirt);
            this->resources.AddMinerals(dug.minerals);

            /* If we didn't use a torch pointing roughly in the right way, we don't move in the frame of digging*/
            if (!(this->turret.IsShooting() &&
                  Direction::FromSpeed(Speed{int(std::round(this->turret.GetDirection().x)),
                                             int(std::round(this->turret.GetDirection().y))}) == dir))
            {
                return;
            }

            /* Now if we used a torch, test the collision again - we might have failed to dig some of the minerals */
            collision = this->GetCollision(dir, this->pos + 1 * this->speed, tl);
            if (collision != CollisionType::None)
                return;
        }

        /* We're free to move, do it*/
        this->direction = Direction{dir};
        this->pos.x += this->speed.x;
        this->pos.y += this->speed.y;

        /* Well, we moved, so let's charge ourselves: */
        this->AlterEnergy(tweak::tank::MoveCost);
    }
}

/* Check to see if we're in any bases, and heal based on that: */
void Tank::TryBaseHeal()
{
    BaseCollision c = this->level->CheckBaseCollision({this->pos.x, this->pos.y}, this->color);
    if (c == BaseCollision::Yours)
    {
        this->AlterEnergy(tweak::tank::HomeChargeSpeed);
        this->AlterHealth(tweak::tank::HomeHealSpeed);
    }
    else if (c == BaseCollision::Enemy)
        this->AlterEnergy(tweak::tank::EnemyChargeSpeed);
}

void Tank::CollectItems()
{
    this->ForEachTankPixel([this](Position world_position) {
        LevelPixel pixel = this->level->GetPixel(world_position);
        if (Pixel::IsEnergy(pixel))
        {
            this->level->SetPixel(world_position, LevelPixel::Blank);
            int energy_collected = 100;
            if (pixel == LevelPixel::EnergyMedium)
                energy_collected *= 2;
            else if (pixel == LevelPixel::EnergyHigh)
                energy_collected *= 4;
            this->AlterEnergy(energy_collected);
        }
        return true;
    });
}

void Tank::Draw(Surface * surface) const
{
    if (!this->health)
        return;

    for (int y = 0; y < 7; y++)
        for (int x = 0; x < 7; x++)
        {
            char val = TANK_SPRITE[this->direction][y][x];
            if (val)
                surface->SetPixel(Position{this->pos.x + x - 3, this->pos.y + y - 3},
                                   Palette.GetTank(this->color)[val - 1]);
        }

    this->turret.Draw(surface);
}

void Tank::AlterEnergy(int diff)
{
    /* You can't alter energy if the tank is dead: */
    if (this->IsDead())
        return;

    /* If the diff would make the energy negative, then we just set it to 0: */
    if (diff < 0 && -diff >= this->energy)
    {
        this->energy = 0;
        this->AlterHealth(-tweak::tank::StartingShield);
        return;
    }

    /* Else, just add, and account for overflow: */
    this->energy = std::min(this->energy + diff, tweak::tank::StartingFuel);
}

void Tank::AlterHealth(int diff)
{
    /* Make sure we don't come back from the dead: */
    if (this->IsDead())
        return;

    /* Die if it's our time (health would be less than 1) */
    if (diff < 0 && -diff >= this->health)
    {
        Die();
        return;
    }

    /* Apply new health */
    this->health = std::min(this->health + diff, tweak::tank::StartingShield);
}

void Tank::Spawn()
{
    this->turret.Reset();

    this->health = tweak::tank::StartingShield;
    this->energy = tweak::tank::StartingFuel;

    this->pos = this->tank_base->GetPosition();
}

void Tank::Die()
{
    /* Begin respawn timer and trigger a nice explosion */
    this->health = 0;
    this->energy = 0;
    this->respawn_timer.Restart();

    this->projectile_list->Add(ExplosionDesc::AllDirections(this->pos, tweak::explosion::death::ShrapnelCount,
                                                            tweak::explosion::death::Speed,
                                                            tweak::explosion::death::Frames)
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
            this->crosshair->SetScreenPosition(controls.crosshair_screen_pos);
        }
        else
        {
            this->crosshair->SetRelativePosition(this, controls.crosshair_direction);
        }
    }
}

bool Tank::IsDead() const { return this->health <= 0; }
