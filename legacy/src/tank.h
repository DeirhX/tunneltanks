#pragma once
#include "collision_component.h"
#include "controllable.h"
#include "controller.h"
#include "gui_widgets.h"
#include "Terrain.h"
#include "level_view.h"
#include "machine_materializer.h"
#include "render_component.h"
#include "tank_sprites.h"
#include "tank_turret.h"
namespace crust
{

class TankBase;
struct LevelView;
class World;

/* Put inside a structure, so we are protected from casual AI cheating: */
struct PublicTankInfo
{
    HealthAmount health;
    EnergyAmount energy;
    Offset relative_pos; /* relative from home base */
    LevelView level_view;

    PublicTankInfo(const Controllable & controllable, Position position_relative_to);
};

namespace tank
{
    template <BasicVisitor<Position> PerPixelFunc>
    void ForEachTankPixel(PerPixelFunc per_pixel_func, const components::BitmapCollision & collider,
                          Position position, Direction direction);
} // namespace tank

class Tank : public Controllable
{
    using Base = Controllable;
    aspect::entity_aspects<aspects::PaletteRenderable> aspects;

    TankColor color;                /* Unique id and also color of the tank */
    TankBase * tank_base = nullptr; /* Base owned by the tank  */
    TankTurret turret;              /* Turret of the tank */
    MachineMaterializer materializer;
    widgets::Crosshair * crosshair = nullptr; /* Crosshair used for aiming */

    ManualTimer respawn_timer = {tweak::tank::RespawnDelay};
    int lives_left = tweak::tank::MaxLives;

    //ProjectileList * projectile_list;

  public:
    Tank(TankColor color, Terrain * level, TankBase * tank_base);

    void SetCrosshair(widgets::Crosshair * cross);

    [[nodiscard]] PositionF GetTurretBarrel() const { return this->turret.GetBarrelPosition(); }
    [[nodiscard]] DirectionF GetTurretDirection() const { return this->turret.GetDirection(); }
    [[nodiscard]] TankColor GetColor() const { return this->color; }
    [[nodiscard]] TankBase * GetBase() const { return this->tank_base; }
    [[nodiscard]] int GetLives() const { return this->lives_left; }

    void Advance(World & world) override; /* Advance world-step */

    void Spawn();
    void Die() override;

    void ApplyControllerOutput(ControllerOutput controls) override;

    CollisionType TryCollide(Direction rotation, Position position) override;

    template <BasicVisitor<Position>
                  PerPixelFunc> /* bool per_pixel_func(Position world_position). Return false to end iteration. */
    void ForEachTankPixel(PerPixelFunc per_pixel_func)
    {
        tank::ForEachTankPixel(per_pixel_func, entity.get_component<components::BitmapCollision>(),
                               GetPosition(), GetDirection().ToIntDirection());
    }

    void Draw(Surface & surface) const;

  private:
    void TryBaseHeal(TankBase & base);

    void AdvanceDeath(World & world);
    void TransferResourcesToBase(TankBase & base);
    void CollectItems();
};

namespace tank
{
    /* return false to stop enumeration */
    template <BasicVisitor<Position> PerPixelFunc>
    void ForEachTankPixel(PerPixelFunc per_pixel_func, const components::BitmapCollision & collider,
                          Position position, Direction direction)
    {
        auto shape = collider.GetForDirection(direction);
        Offset offset;
        for (offset.y = 0; offset.y < collider.Size().y; ++offset.y)
            for (offset.x = 0; offset.x < collider.Size().x; ++offset.x)
            {
                if (shape.GetAt(offset))
                    if (!per_pixel_func(position - collider.Center() + offset))
                        return;
            }
    }
} // namespace tank
} // namespace crust