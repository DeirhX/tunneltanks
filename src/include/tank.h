#pragma once
#include <memory>
/* For the controllers/AIs: */

#include <level.h>
#include <level_view.h>
#include <projectile_list.h>


#include "controller.h"
#include "gui_widgets.h"
#include "machine_materializer.h"
#include "tank_turret.h"
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

class Resources
{
    int dirt = {};
    int minerals = {};
public:
    bool PayDirt(int amount);
    bool PayMinerals(int amount);
    bool Pay(int pay_dirt, int pay_minerals);
    void AddMinerals(int amount) { this->minerals += amount; }
    void AddDirt(int amount) { this->dirt += amount; }
    int GetDirt() const { return this->dirt; }
    int GetMinerals() const { return this->minerals; }
};


class Tank 
{
    bool is_valid = false;

    Position pos; /* Current tank position */
    Speed speed;  /* Velocity... ie: is it moving now? */
    Direction direction = {}; /* Heading of the tank */

    TankColor color;                          /* Unique id and also color of the tank */
    TankBase * tank_base = nullptr;           /* Base owned by the tank  */
    TankTurret turret;                        /* Turret of the tank */
    MachineMaterializer materializer;
    widgets::Crosshair * crosshair = nullptr; /* Crosshair used for aiming */

    int respawn_timer = 0;

    int health = tweak::tank::StartingShield;
    int energy = tweak::tank::StartingFuel;
    int lives_left = tweak::tank::MaxLives;

    Resources resources = {};

    std::shared_ptr<Controller> controller = nullptr;

    Level * level;
    ProjectileList * projectile_list;

  public:
    void Invalidate() { this->is_valid = false; }

    Tank(TankColor color, Level * level, ProjectileList * projectile_list, TankBase * tank_base);
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
    [[nodiscard]] const Resources& GetResources() const { return this->resources; }
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
