#pragma once
#include "controller.h"
#include "gui_widgets.h"
#include "level.h"
#include "level_view.h"
#include "machine_materializer.h"
#include "projectile_list.h"
#include "tank_sprites.h"
#include "tank_turret.h"
#include "weapon.h"
#include <memory>

class TankBase;
struct LevelView;
class World;

/* Put inside a structure, so we are protected from casual AI cheating: */
struct PublicTankInfo
{
    HealthAmount health;
    EnergyAmount energy;
    int x, y; /* relative from home base */
    LevelView level_view;
};

enum class CollisionType
{
    None,   /* All's clear! */
    Dirt,   /* We hit dirt, but that's it. */
    Blocked /* Hit a rock/base/tank/something we can't drive over. */
};

namespace tank
{
template <typename PerPixelFunc>
requires concepts::BasicVisitor<PerPixelFunc, Position>
void ForEachTankPixel(PerPixelFunc per_pixel_func, Position position, Direction direction);
} // namespace tank

class Tank : public Invalidable
{
    Position position;        /* Current tank position */
    Speed speed;              /* Velocity... ie: is it moving now? */
    Direction direction = {}; /* Heading of the tank */

    TankColor color;                /* Unique id and also color of the tank */
    TankBase * tank_base = nullptr; /* Base owned by the tank  */
    TankTurret turret;              /* Turret of the tank */
    MachineMaterializer materializer;
    widgets::Crosshair * crosshair = nullptr; /* Crosshair used for aiming */

    ManualTimer respawn_timer = {tweak::tank::RespawnDelay};
    int lives_left = tweak::tank::MaxLives;

    LinkPointSource link_source;
    Reactor reactor = tweak::tank::DefaultTankReactor;
    MaterialContainer resources = {tweak::tank::ResourcesMax};

    std::shared_ptr<Controller> controller = nullptr;

    Level * level;
    ProjectileList * projectile_list;

  public:
    Tank(TankColor color, Level * level, ProjectileList * projectile_list, TankBase * tank_base);

    void SetController(std::shared_ptr<Controller> newController) { this->controller = newController; }
    void SetCrosshair(widgets::Crosshair * cross);

    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] PositionF GetTurretBarrel() const { return this->turret.GetBarrelPosition(); }
    [[nodiscard]] DirectionF GetTurretDirection() const { return this->turret.GetDirection(); }
    [[nodiscard]] TankColor GetColor() const { return this->color; }
    [[nodiscard]] DirectionF GetDirection() const { return this->direction; }
    [[nodiscard]] TankBase * GetBase() const { return this->tank_base; }
    [[nodiscard]] MaterialContainer & GetResources() { return this->resources; }
    [[nodiscard]] Reactor & GetReactor() { return this->reactor; }
    [[nodiscard]] Level * GetLevel() const { return this->level; };

    [[nodiscard]] bool IsDead() const;
    [[nodiscard]] int GetEnergy() const { return this->reactor.GetEnergy(); }
    [[nodiscard]] int GetHealth() const { return this->reactor.GetHealth(); }
    [[nodiscard]] int GetLives() const { return this->lives_left; }

    void Advance(World * world); /* Advance world-step */

    void Spawn();
    void Die();

    void ApplyControllerOutput(ControllerOutput controls);

    CollisionType GetCollision(Direction dir, Position pos, TankList * tank_list);
    template <typename PerPixelFunc> /* bool per_pixel_func(Position world_position). Return false to end iteration. */
    requires concepts::BasicVisitor<PerPixelFunc, Position>
    void ForEachTankPixel(PerPixelFunc per_pixel_func)
    {
        tank::ForEachTankPixel(per_pixel_func, this->position, this->direction);
    }

    void Draw(Surface * surface) const;

  private:
    void HandleMove(class TankList * tank_list);
    void TryBaseHeal(TankBase * base);
    void TransferResourcesToBase(TankBase * base);
    void CollectItems();
};

namespace tank
{
/* return false to stop enumeration */
template <typename PerPixelFunc>
requires concepts::BasicVisitor<PerPixelFunc, Position>
void ForEachTankPixel(PerPixelFunc per_pixel_func, Position position, Direction direction)
{
    Offset offset;
    for (offset.y = -3; offset.y <= 3; offset.y++)
        for (offset.x = -3; offset.x <= 3; offset.x++)
        {
            if (TANK_SPRITE[direction][3 + offset.y][3 + offset.x])
            {
                if (!per_pixel_func(position + offset))
                    return;
            }
        }
}
} // namespace tank