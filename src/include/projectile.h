#pragma once

#include "mymath.h"
#include "types.h"
#include <containers.h>
#include <vector>

#include "random.h"
#include "colors.h"
#include "tweak.h"

namespace math
{
struct Radians;
}

enum class ProjectileType
{
    Invalid,
    Bullet,
    Explosion,
    Shrapnel,
    ConcreteBarrel,
    DirtBarrel,
};

/* Projectile base class. Doesn't do much, just adds shared member.  */
/* TODO: We're not doing to use polymorphism so do we really need it? */
struct Projectile
{
    PositionF pos;
    SpeedF speed;
    bool is_alive = false;
    class Level * level;

  private:
    Projectile() = default; // Never use manually. Will be used inside intrusive containers
  protected:
    Projectile(Position position, SpeedF speed, Level * level)
        : pos(position), speed(speed.x, speed.y), is_alive(true), level(level)
    {
    }

  public:
    virtual ~Projectile() = default;
    //virtual ProjectileType GetType() = 0;
    virtual void Advance(class TankList * tankList) = 0;
    virtual void Draw(class LevelPixelSurface * drawBuffer) = 0;
    virtual void Erase(LevelPixelSurface * drawBuffer, Level * level) = 0;

    bool IsInvalid() const { return !is_alive; }
    bool IsValid() const { return is_alive; }
    void Invalidate() { is_alive = false; }
};

/*  
 *  Shrapnel
 * 
 *  Non-damaging, non-collidable projectile in form of one pixel.
 */
class Shrapnel : public Projectile
{
  protected:
    int life = 0;

  public:
    Shrapnel(Position position, SpeedF speed, int life, Level * level) : Projectile(position, speed, level), life(life)
    {
    }
    //ProjectileType GetType() override { return ProjectileType::Shrapnel; }

    void Advance(class TankList * tankList) override;
    void Draw(class LevelPixelSurface * drawBuffer) override;
    void Erase(LevelPixelSurface * drawBuffer, Level * level) override;

  protected:
    template <typename OnAdvanceFuncType>
    void AdvanceShrapnel(TankList * tankList, OnAdvanceFuncType OnAdvanceFunc);
};

/*
 * ConcreteFoam
 *
 * Flying concrete from exploded ConcreteBarrel, seeking to attach concrete to surfaces
 */
class ConcreteFoam : public Shrapnel
{
  public:
    ConcreteFoam(Position position, SpeedF speed, int life, Level * level) : Shrapnel(position, speed, life, level) {}
    void Advance(class TankList * tankList) override;
    void Draw(class LevelPixelSurface * drawBuffer) override;
};

/*
 * DirtFoam
 *
 * Flying dirt, result of DirtBarrel
 */
class DirtFoam : public Shrapnel
{
  public:
    DirtFoam(Position position, SpeedF speed, int life, Level * level) : Shrapnel(position, speed, life, level) {}
    void Advance(class TankList * tankList) override;
    void Draw(class LevelPixelSurface * drawBuffer) override;
};

/*
 * MotionBlurProjectile
 *
 * Base class for projectiles that leaves a trail
 */
class MotionBlurProjectile : public Projectile
{
  public:
    PositionF pos_blur_from; /* The x,y of the 'cold' portion. (#ba0000) */
  protected:
    MotionBlurProjectile(Position position, SpeedF speed, Level * level) : Projectile(position, speed, level) {}
};

/*
 * Bullet
 *
 * Basic cannot bullet shot by a tank
 */
class Bullet : public MotionBlurProjectile
{
    using Base = MotionBlurProjectile;

  public:
    class Tank * tank;

  public:
    Bullet(Position position, SpeedF speed, Level * level, Tank * tank) : Base(position, speed, level), tank(tank) {}
    //ProjectileType GetType() override { return ProjectileType::Bullet; }

    void Advance(class TankList * tankList) override;
    void Draw(class LevelPixelSurface * drawBuffer) override;
    void Erase(LevelPixelSurface * drawBuffer, Level * level) override;
};

/*
 * FlyingBarrel
 *
 * Will explode before surfaces to create ConcreteSpray that will add a concrete layer adjacent to it
 */
class FlyingBarrel : public Projectile
{
    using Base = Projectile;
    class Tank * tank;
    Color32 draw_color;
    int explode_distance;
  public:
    FlyingBarrel(Position position, SpeedF speed, Level * level, Tank * tank, Color32 draw_color, int explode_distance)
        : Base(position, speed, level), tank(tank), draw_color(draw_color), explode_distance(explode_distance)
    {
    }
    template <typename ExplosionFuncType>
    void Advance(TankList * tankList, ExplosionFuncType explosionFunc);

    void Draw(LevelPixelSurface * drawBuffer) override;
    void Erase(LevelPixelSurface * drawBuffer, Level * level) override;
};

/*
 * ConcreteBarrel
 * Flying barrel exploding in a fan of concrete
 */

class ConcreteBarrel : public FlyingBarrel
{
public:
    ConcreteBarrel(Position position, SpeedF speed, Level * level, Tank * tank)
    : FlyingBarrel(position, speed, level, tank, Palette.Get(Colors::ConcreteShot), tweak::weapon::ConcreteDetonationDistance)
  {
  }
  void Advance(TankList * tankList) override;
  
};

/*
 * DirtBarrel
 * Flying barrel exploding in a fan of dirt
 */
class DirtBarrel : public FlyingBarrel
{
  public:
    DirtBarrel(Position position, SpeedF speed, Level * level, Tank * tank)
        : FlyingBarrel(position, speed, level, tank, Palette.Get(Colors::DirtContainerShot),
                       tweak::weapon::DirtDetonationDistance)
    {
    }
    void Advance(TankList * tankList) override;
};
/*
 * ExplosionDesc
 *
 * Descriptors of various explosions
 */
struct ExplosionDesc
{
    Position center = {};
    DirectionF base_direction = {};
    math::Radians direction_spread = {};
    int shrapnel_count = 0;
    float speed_min = 0;
    float speed_max = 0;
    int frames_length_min = 0;
    int frames_length_max = 0;

    template <typename ShrapnelType>
    std::vector<ShrapnelType> Explode(class Level * level) const;

    static ExplosionDesc AllDirections(Position pos, int shrapnel_count, float speed, int frames_length)
    {
        return ExplosionDesc{.center = pos,
                             .base_direction = {1.f, 0.f},
                             .direction_spread = math::two_pi,
                             .shrapnel_count = shrapnel_count,
                             .speed_min = speed * 0.1f,
                             .speed_max = speed,
                             .frames_length_min = 0,
                             .frames_length_max = frames_length};
    }

    static ExplosionDesc Fan(Position pos, DirectionF direction, math::Radians angle, int shrapnel_count, float speed,
                             int frames_length)
    {
        return ExplosionDesc{.center = pos,
                             .base_direction = direction,
                             .direction_spread = angle,
                             .shrapnel_count = shrapnel_count,
                             .speed_min = speed,
                             .speed_max = speed,
                             .frames_length_min = frames_length,
                             .frames_length_max = frames_length};
    }
};

template <typename ShrapnelType>
std::vector<ShrapnelType> ExplosionDesc::Explode(Level * level) const
{
    auto items = std::vector<ShrapnelType>{};
    items.reserve(this->shrapnel_count);
    /* Add all of the effect particles: */
    for (int i = 0; i < this->shrapnel_count; i++)
    {
        auto base_rads = math::Radians{this->base_direction};
        auto chosen_rads = math::Radians{Random.Float(base_rads.val - this->direction_spread.val / 2,
                                                      base_rads.val + this->direction_spread.val / 2)};
        auto chosen_speed = Random.Float(this->speed_min, this->speed_max) * tweak::explosion::MadnessLevel;

        items.emplace_back(ShrapnelType{this->center, chosen_rads.ToDirection() * chosen_speed,
                                        Random.Int(this->frames_length_min, this->frames_length_max), level});
    }
    return items;
}
