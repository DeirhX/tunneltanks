#include "pch.h"
#include "tank_turret.h"
#include "controller.h"
#include "duration.h"
#include "terrain.h"
#include "raycaster.h"
#include "render_surface.h"
#include "tank.h"
namespace crust
{
TankTurret::TankTurret(Tank * owner, Color turret_color)
    : entity(entities.registry.create_entity()), tank(owner), color(turret_color)
{
    this->entity.assign_component<PositionF>(owner->GetPosition());
    this->entity.assign_component<DirectionF>(1.0f, 0.f);
    //this->render_line = &this->entity.assign_component<RenderLine>(turret_color, PositionF(owner->GetPosition()));
    //this->tank->GetPosition()
    Reset();
}

PositionF TankTurret::GetBarrelPosition() const
{
    return PositionF(this->tank->GetPosition()) + (this->GetDirection() * float(tweak::tank::TurretLength));
}

void TankTurret::ApplyControllerOutput(ControllerOutput controls)
{
    this->is_shooting_primary = controls.shoot.is_shooting_primary;
    this->is_shooting_secondary = controls.shoot.is_shooting_secondary;
    this->is_shooting_tertiary = controls.shoot.is_shooting_tertiary;

    if (controls.shoot.switch_primary_weapon_next)
        this->primary_weapon.CycleNext();
    if (controls.shoot.switch_primary_weapon_prev)
        this->primary_weapon.CyclePrevious();
    if (controls.shoot.switch_secondary_weapon_next)
        this->secondary_weapon.CycleNext();
    if (controls.shoot.switch_secondary_weapon_prev)
        this->secondary_weapon.CyclePrevious();
}

void TankTurret::Advance(Position tank_position, widgets::Crosshair * crosshair)
{
    DirectionF & direction = entity.get_component<DirectionF>();
    if (crosshair)
    { /* If we got a crosshair at action, let it dictate the direction */
        Position crosshair_pos = crosshair->GetWorldPosition();
        auto turret_dir = OffsetF(crosshair_pos - tank_position);
        if (turret_dir != OffsetF{})
            direction = DirectionF::FromAbnormal(turret_dir);
    }
    /* If we inherited it from tank it needs to be normalized. So do it just in case, cheaper than querying. */
    else
    {
        direction = DirectionF{direction.Normalize()};
    }
    OffsetF offset = direction * tweak::tank::TurretLength;
    //this->render_line->endpoint = 

    /* Begin the turret at voxel 0 */
    int turret_len = 0;
    this->TurretVoxels[turret_len++] = tank_position;

    auto visitor = [this, &turret_len](PositionF current, PositionF)
    {
        if (turret_len >= tweak::tank::TurretLength)
            return false;

        this->TurretVoxels[turret_len++] = current.ToIntPosition();
        return true;
    };
    Raycaster::Cast(PositionF(tank_position), GetBarrelPosition(), visitor,
                    Raycaster::VisitFlags::PixelsMustTouchCorners);

    this->current_length = turret_len;
}

void TankTurret::Draw(Surface & surface) const
{
    for (int i = 0; i < this->current_length; ++i)
    {
        surface.SetPixel(this->TurretVoxels[i], this->color);
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
                this->bullet_timer =
                    this->primary_weapon.Fire(this->tank->GetPosition(), this->GetDirection(), this->tank);
            /* We just fired. Let's charge ourselves: */
        }
        if (this->is_shooting_secondary)
        {
            if (this->tank->GetReactor().Pay(tweak::tank::ShootCost))
                this->bullet_timer =
                    this->secondary_weapon.Fire(this->tank->GetPosition(), this->GetDirection(), this->tank);
        }
        if (this->is_shooting_tertiary)
        {
            if (this->tank->GetReactor().Pay(tweak::tank::ShootCost))
                this->bullet_timer =
                    this->tertiary_weapon.Fire(this->tank->GetPosition(), this->GetDirection(), this->tank);
        }
    }
    else
        --this->bullet_timer;
}

void TankTurret::Reset() { this->bullet_timer = Duration::Zero(); }
} // namespace crust