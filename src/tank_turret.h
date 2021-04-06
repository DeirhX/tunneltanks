#pragma once
#include <array>

#include "color.h"
#include "duration.h"
#include "types.h"
#include "weapon.h"
#include "gui_widgets.h"

class Terrain;
class Surface;
struct ControllerOutput;

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
    Duration bullet_timer = Duration{};
    //int bullets_left = tweak::tank::BulletMax;

    Weapon primary_weapon = Weapon{WeaponType::Cannon};
    Weapon secondary_weapon = Weapon{WeaponType::ConcreteSpray};
    Weapon tertiary_weapon = Weapon{WeaponType::DirtSpray};

  public:
    TankTurret(Tank * owner, Color turret_color) : tank(owner), color(turret_color) { Reset(); }
    DirectionF GetDirection() const { return this->direction; }
    PositionF GetBarrelPosition() const;
    bool IsShooting() const
    {
        return this->is_shooting_primary || this->is_shooting_secondary || this->is_shooting_tertiary;
    }
    void ApplyControllerOutput(ControllerOutput controls);

    void Advance(Position tank_position, widgets::Crosshair * crosshair);
    void Draw(Surface * surface) const;
    void SetDirection(DirectionF new_dir) { this->direction = new_dir; }

    void HandleShoot();
    void Reset();
};
