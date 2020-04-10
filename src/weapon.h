#pragma once
#include "tweak.h"
#include "types.h"

class Tank;

enum class WeaponType
{
    Cannon,
    ConcreteSpray,
    DirtSpray,
    Size,
};
//
//struct WeaponDescriptor
//{
//    DurationFrames cooldown_speed;
//
//    void SpawnProjectile(Position pos, DirectionF direction, Tank * tank) { assert(!"Base class"); };
//};
//
//struct WeaponCannonDescriptor : public WeaponDescriptor
//{
//    WeaponCannonDescriptor() : WeaponDescriptor({.cooldown_speed = DurationFrames{tweak::weapon::CannonBulletSpeed}}) {}
//
//    void SpawnProjectile(Position pos, DirectionF direction, Tank * tank);
//};
//
//struct WeaponConcreteSprayDescriptor : public WeaponDescriptor
//{
//    WeaponConcreteSprayDescriptor()
//        : WeaponDescriptor({.cooldown_speed = DurationFrames{tweak::weapon::ConcreteSpraySpeed}})
//    {
//    }
//
//    void SpawnProjectile(Position pos, DirectionF direction, Tank * tank);
//};
//
//struct WeaponDescriptors
//{
//    WeaponCannonDescriptor cannon;
//    WeaponConcreteSprayDescriptor concrete_spray;
//};

class Weapon
{
    WeaponType current;

  public:
    Weapon(WeaponType new_type) : current(new_type) {}
    WeaponType GetType() const { return current; }
    void SetType(WeaponType new_type) { current = new_type; }
    void CycleNext();
    void CyclePrevious();

    Duration Fire(Position pos, DirectionF direction, Tank * tank);
};