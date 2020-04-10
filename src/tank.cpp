#include "base.h"
#include <algorithm>

#include <level.h>
#include <level_view.h>
#include <projectile.h>
#include <random.h>
#include <screen.h>
#include <tank.h>
#include <tanklist.h>
#include <tanksprites.h>
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

bool Resources::Pay(int pay_dirt, int pay_minerals)
{
    if (this->dirt - pay_dirt < 0 || this->minerals - pay_minerals < 0)
        return false;
    this->dirt -= pay_dirt;
    this->minerals -= pay_minerals;
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

        /* Place buildings */
        

        /* Rotate the turret temporarily back directly to match our heading direction */
        if (this->speed.x || this->speed.y)
        {
            this->turret.SetDirection(DirectionF{Direction::FromSpeed(this->speed)});
        }
        /* Now override this by rotating according to crosshair if it is being used */
        this->turret.Advance(this->GetPosition(), this->crosshair);

        /* Move, dig and solve collisions with other tanks */
        this->HandleMove(world->GetTankList());
        
        /* Shoot the turret if desired */
        this->turret.HandleShoot();
    }
    else
    {
        /* DEaD. Handle respawning. */
        if (! --this->respawn_timer)
        {
            if (! --this->lives_left)
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
CollisionType Tank::GetCollision(int dir, Position position, TankList * tl)
{
    Offset off;
    CollisionType out = CollisionType::None;

    /* Level Collisions: */
    for (off.y = -3; off.y <= 3; off.y++)
        for (off.x = -3; off.x <= 3; off.x++)
        {
            char c = TANK_SPRITE[dir][3 + off.y][3 + off.x];
            if (!c)
                continue;

            LevelPixel v = this->level->GetPixel(position + off);

            if (Pixel::IsDirt(v))
                out = CollisionType::Dirt;

            if (Pixel::IsBlockingCollision(v))
                return CollisionType::Blocked;
        }

    /* Tank collisions: */
    if (tl->CheckForCollision(*this, position, dir))
        return CollisionType::Blocked;
    return out;
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
    this->respawn_timer = tweak::tank::RespawnDelay;

    this->projectile_list->Add(ExplosionDesc::AllDirections(this->pos, tweak::explosion::death::ShrapnelCount,
                                                            tweak::explosion::death::Speed,
                                                            tweak::explosion::death::Frames)
                                   .Explode<Shrapnel>(this->level));
}

void Tank::ApplyControllerOutput(ControllerOutput controls)
{
    this->turret.ApplyControllerOutput(controls);
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
