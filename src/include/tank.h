#pragma once
#include <memory>
/* For the controllers/AIs: */

#include <level.h>
#include <level_view.h>
#include <projectile_list.h>


#include "controller.h"
#include "gui_widgets.h"
#include "weapon.h"
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
    class Tank * tank;

    std::array<Position, tweak::tank::TurretLength> TurretVoxels;
    Color color;
    DirectionF direction = {1.0, 0};
    int current_length = tweak::tank::TurretLength;

    bool is_shooting_primary = false;
    bool is_shooting_secondary = false;
    bool is_shooting_tertiary = false;
    DurationFrames bullet_timer = DurationFrames{tweak::tank::TurretDelay};
    //int bullets_left = tweak::tank::BulletMax;

    Weapon primary_weapon = Weapon{WeaponType::Cannon};
    Weapon secondary_weapon = Weapon{WeaponType::ConcreteSpray};
    Weapon tertiary_weapon = Weapon{WeaponType::DirtSpray};

  public:
    TankTurret(Tank * owner, Color turret_color) : tank(owner), color(turret_color) { Reset(); }
    DirectionF GetDirection() const { return this->direction; }
    bool IsShooting() const
    {
        return this->is_shooting_primary || this->is_shooting_secondary || this->is_shooting_tertiary;
    }
    void ApplyControllerOutput(ControllerOutput controls);

    void Advance(Position tank_position, widgets::Crosshair * crosshair);
    void Draw(Surface * drawBuff) const;
    void Erase(Level * level) const;
    void SetDirection(DirectionF new_dir) { this->direction = new_dir; }

    void HandleShoot();
    void Reset();
};

class Tank final
{
    bool is_valid = false;

    Position pos; /* Current tank position */
    Speed speed;  /* Velocity... ie: is it moving now? */
    Direction direction = {}; /* Heading of the tank */

    TankColor color;                          /* Unique id and also color of the tank */
    TankBase * tank_base = nullptr;           /* Base owned by the tank  */
    TankTurret turret;                        /* Turret of the tank */
    widgets::Crosshair * crosshair = nullptr; /* Crosshair used for aiming */

    int respawn_timer = 0;

    int health = tweak::tank::StartingShield;
    int energy = tweak::tank::StartingFuel;
    int lives_left = tweak::tank::MaxLives;
    int dirt_mined = 0;
    int minerals_mined = 0;

    std::shared_ptr<Controller> controller = nullptr;

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
    [[nodiscard]] int GetDirtMined() const { return this->dirt_mined; }
    [[nodiscard]] int GetRockMined() const { return this->minerals_mined; }
    [[nodiscard]] Level * GetLevel() { return this->level; };

    void Advance(World * world); /* Advance world-step */

    void AlterEnergy(int diff);
    void AlterHealth(int diff);

    void Spawn();
    void Die();

    void ApplyControllerOutput(ControllerOutput controls);

    CollisionType GetCollision(int dir, Position pos, TankList * tl);

    void Draw(Surface * surface) const;

  private:
    void HandleMove(class TankList * tl);
    void TryBaseHeal();
};
