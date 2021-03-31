#include "weapon.h"

#include "projectiles.h"
#include "world.h"

#include <cassert>

/*
void WeaponCannonDescriptor::SpawnProjectile(Position pos, DirectionF direction, Tank * tank)
{
    GetWorld()->GetProjectileList()->Add(
        Bullet{pos, direction, tweak::weapon::CannonBulletSpeed, GetWorld()->GetLevel(), tank});
}

void WeaponConcreteSprayDescriptor::SpawnProjectile(Position pos, DirectionF direction, Tank * tank)
{
}
*/

void Weapon::CycleNext()
{
    this->current = WeaponType(((int(this->current) + 1) % int(WeaponType::Size)));
}

void Weapon::CyclePrevious()
{
    int value = int(this->current) - 1;
    if (value < 0)
        this->current = WeaponType{int(WeaponType::Size) - 1};
    else
        this->current = WeaponType{value};
}

Duration Weapon::Fire(Position pos, DirectionF direction, Tank * tank)
{
    switch (this->current)
    {
    case WeaponType::Cannon:
        GetWorld()->GetProjectileList()->Add(
            Bullet{pos, direction * tweak::weapon::CannonBulletSpeed, GetWorld()->GetLevel(), tank});
        return tweak::weapon::CannonCooldown;
    case WeaponType::ConcreteSpray:
        GetWorld()->GetProjectileList()->Add(
            ConcreteBarrel{pos, direction * tweak::weapon::ConcreteBarrelSpeed, GetWorld()->GetLevel(), tank});
        return tweak::weapon::ConcreteSprayCooldown;
    case WeaponType::DirtSpray:
        GetWorld()->GetProjectileList()->Add(
            DirtBarrel{pos, direction * tweak::weapon::DirtBarrelSpeed, GetWorld()->GetLevel(), tank});
        return tweak::weapon::ConcreteSprayCooldown;
    default:
        assert(false);
    }
    return tweak::weapon::CannonCooldown;
}
