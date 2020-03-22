#pragma once
#include <memory>
/* For the controllers/AIs: */

#include <controllersdl.h>
#include <drawbuffer.h>
#include <level.h>
#include <level_view.h>
#include <projectile_list.h>

#include "gui_widgets.h"
//#include <world.h>

struct LevelView;
class World;

/* Put inside a structure, so we are protected from casual AI cheating: */
struct PublicTankInfo
{
    int health, energy;
    int x, y; /* relative from home base */
    LevelView level_view;
};

enum class CollisionType
{
    None,   /* All's clear! */
    Dirt,   /* We hit dirt, but that's it. */
    Blocked /* Hit a rock/base/tank/something we can't drive over. */
};

class TankTurret
{
    std::array<Position, tweak::tank::TurretLength> TurretVoxels;
    Color color;
    DirectionF direction = {1.0, 0};
  public:
    TankTurret(Color turret_color) : color(turret_color) {}
    DirectionF GetDirection() const { return this->direction; }

    void Advance(Position tank_position, widgets::Crosshair * crosshair); 
    void Draw(LevelDrawBuffer * drawBuff) const;
    void Erase(Level * level) const;
    void SetDirection(DirectionF new_dir) { this->direction = new_dir; }
};

class Tank final
{
    bool is_valid = false;
    bool is_shooting = false;

    Position pos; /* Current tank position */
    Speed speed;  /* Velocity... ie: is it moving now? */
    Direction direction = {};

    TankColor color;                /* Unique id and also color of the tank */
    TankBase * tank_base = nullptr; /* Base owned by the tank  */
    TankTurret turret;              /* Turret of the tank */
    widgets::Crosshair * crosshair = nullptr; /* Crosshair used for aiming */


    int bullet_timer = tweak::tank::BulletDelay;
    int bullets_left = tweak::tank::BulletMax;
    int respawn_timer = 0;

    int health = tweak::tank::StartingShield;
    int energy = tweak::tank::StartingFuel;
    int lives_left = tweak::tank::MaxLives;

    std::shared_ptr<Controller> controller;

    Level * level;
    ProjectileList * projectile_list;

  public:
    void Invalidate() { this->is_valid = false; }

    Tank(TankColor color, Level * lvl, ProjectileList * pl, TankBase * tank_base);
    void SetController(std::shared_ptr<Controller> newController) { this->controller = newController; }
    void SetCrosshair(widgets::Crosshair * cross);

    [[nodiscard]] Position GetPosition() const { return this->pos; }
    [[nodiscard]] TankColor GetColor() const { return this->color; }
    [[nodiscard]] DirectionF GetDirection() const { return this->direction; }

    [[nodiscard]] bool IsDead() const;
    [[nodiscard]] bool IsValid() const { return this->is_valid; }    // For ValueContainer
    [[nodiscard]] bool IsInvalid() const { return !this->is_valid; } // For ValueContainer
    [[nodiscard]] int GetEnergy() const { return this->energy; }
    [[nodiscard]] int GetHealth() const { return this->health; }
    [[nodiscard]] int GetLives() const { return this->lives_left; }
    [[nodiscard]] Level * GetLevel() { return this->level; };

    void Advance(World * world); /* Advance world-step */

    void AlterEnergy(int diff);
    void AlterHealth(int diff);

    void Spawn();
    void Die();

    void ApplyControllerOutput(ControllerOutput controls);

    CollisionType GetCollision(int dir, Position pos, TankList * tl);

    void Clear(LevelDrawBuffer * drawBuff) const;
    void Draw(LevelDrawBuffer * drawBuff) const;

    void ReturnBullet();

  private:
    void HandleMove(class TankList * tl);
    void HandleShoot();
    void TryBaseHeal();
};
