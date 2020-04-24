#include "tank_turret.h"
#include "controller.h"
#include "duration.h"
#include "level.h"
#include "raycaster.h"
#include "render_surface.h"
#include "tank.h"


PositionF TankTurret::GetBarrelPosition() const
{
    return PositionF(this->tank->GetPosition()) + (this->direction * float(tweak::tank::TurretLength));
}

void TankTurret::ApplyControllerOutput(ControllerOutput controls)
{
    this->is_shooting_primary = controls.is_shooting_primary;
    this->is_shooting_secondary = controls.is_shooting_secondary;
    this->is_shooting_tertiary = controls.is_shooting_tertiary;

    if (controls.switch_primary_weapon_next)
        this->primary_weapon.CycleNext();
    if (controls.switch_primary_weapon_prev)
        this->primary_weapon.CyclePrevious();
    if (controls.switch_secondary_weapon_next)
        this->secondary_weapon.CycleNext();
    if (controls.switch_secondary_weapon_prev)
        this->secondary_weapon.CyclePrevious();
}

void TankTurret::Advance(Position tank_position, widgets::Crosshair * crosshair)
{
    if (crosshair)
    { /* If we got a crosshair at action, let it dictate the direction */
        Position crosshair_pos = crosshair->GetWorldPosition();
        auto turret_dir = OffsetF(crosshair_pos - tank_position);
        if (turret_dir != OffsetF{})
            this->direction = DirectionF{turret_dir};
    }
    /* If we inherited it from tank it needs to be normalized. So do it just in case, cheaper than querying. */
    this->direction = DirectionF{this->direction.Normalize()};

    /* Begin the turret at voxel 0 */
    int turret_len = 0;
    this->TurretVoxels[turret_len++] = tank_position;

    auto visitor = [this, &turret_len](PositionF current, PositionF) {
        if (turret_len >= tweak::tank::TurretLength)
            return false;

        this->TurretVoxels[turret_len++] = current.ToIntPosition();
        return true;
    };
    Raycaster::Cast(PositionF(tank_position),
                    GetBarrelPosition(), visitor,
                    Raycaster::VisitFlags::PixelsMustTouchCorners);

    this->current_length = turret_len;
}

void TankTurret::Draw(Surface * surface) const
{
    for (int i = 0; i < this->current_length; ++i)
    {
        surface->SetPixel(this->TurretVoxels[i], this->color);
    }
}

void TankTurret::Erase(Level * level) const
{
    for (const Position & pos : this->TurretVoxels)
    {
        level->CommitPixel(pos);
    }
}

void TankTurret::HandleShoot()
{
    /* Handle all shooting logic: */
    if (this->bullet_timer.Finished())
    {
        if (this->is_shooting_primary)
        {
            if (this->tank->GetReactor().Pay(tweak::tank::ShootCost))
                this->bullet_timer = this->primary_weapon.Fire(this->tank->GetPosition(), this->GetDirection(), this->tank);
            /* We just fired. Let's charge ourselves: */
        }
        if (this->is_shooting_secondary)
        {
            if (this->tank->GetReactor().Pay(tweak::tank::ShootCost))
                this->bullet_timer = this->secondary_weapon.Fire(this->tank->GetPosition(), this->GetDirection(), this->tank);
        }
        if (this->is_shooting_tertiary)
        {
            if (this->tank->GetReactor().Pay(tweak::tank::ShootCost))
                this->bullet_timer = this->tertiary_weapon.Fire(this->tank->GetPosition(), this->GetDirection(), this->tank);
        }
    }
    else
        --this->bullet_timer;
}

void TankTurret::Reset() { this->bullet_timer = Duration::Zero(); }