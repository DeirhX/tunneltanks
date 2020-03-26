#pragma once

#include "types.h"
#include <containers.h>
#include <vector>
#include "mymath.h"

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
    Concrete,
};

/* Projectile base class */
struct Projectile
{
    PositionF pos;
    SpeedF direction;
    bool is_alive = false;
    class Level * level;

  private:
    Projectile() = default; // Never use manually. Will be used inside intrusive containers
  protected:
    Projectile(Position position, SpeedF speed, Level * level)
        : pos(position), direction(speed.x, speed.y), is_alive(true), level(level)
    {
    }

  public:
    virtual ~Projectile() = default;
    virtual ProjectileType GetType() = 0;
    virtual void Advance(class TankList * tankList) = 0;
    virtual void Draw(class LevelDrawBuffer * drawBuffer) = 0;
    virtual void Erase(LevelDrawBuffer * drawBuffer, Level * level) = 0;

    bool IsInvalid() const { return !is_alive; }
    bool IsValid() const { return is_alive; }
    void Invalidate() { is_alive = false; }
};

/* Non-damaging, non-collidable projectile spawned by the environment */
class Shrapnel : public Projectile
{
    int life = 0;

  public:
    //Shrapnel() = default;
    Shrapnel(Position position, SpeedF speed, int life, Level * level) : Projectile(position, speed, level), life(life)
    {
    }
    ProjectileType GetType() override { return ProjectileType::Shrapnel; }

    void Advance(class TankList * tankList) override;
    void Draw(class LevelDrawBuffer * drawBuffer) override;
    void Erase(LevelDrawBuffer * drawBuffer, Level * level) override;
};

/* Projectile that leaves a trail */
class MotionBlurProjectile : public Projectile
{
  public:
    PositionF pos_blur_from; /* The x,y of the 'cold' portion. (#ba0000) */
  protected:
    MotionBlurProjectile(Position position, SpeedF speed, Level * level) : Projectile(position, speed, level) {}
};

/* Projectile shot by a tank */
class Bullet : public MotionBlurProjectile
{
    using Base = MotionBlurProjectile;
    int simulation_steps = 0;

  public:
    class Tank * tank;

  public:
    Bullet(Position position, SpeedF speed, int simulation_steps, Level * level, Tank * tank)
        : Base(position, speed, level), simulation_steps(simulation_steps), tank(tank)
    {
    }
    ProjectileType GetType() override { return ProjectileType::Bullet; }

    void Advance(class TankList * tankList) override;
    void Draw(class LevelDrawBuffer * drawBuffer) override;
    void Erase(LevelDrawBuffer * drawBuffer, Level * level) override;
};

class ConcreteSpray : public Projectile
{
    using Base = Projectile;
    class Tank * tank;
    constexpr static float flight_speed = 2.f;

  public:
    ConcreteSpray(Position position, SpeedF speed, Level * level, Tank * tank)
        : Base(position, speed, level), tank(tank)
    {
    }
    ProjectileType GetType() override { return ProjectileType::Concrete; }
    void Advance(TankList * tankList) override;
    void Draw(LevelDrawBuffer * drawBuffer) override;
    void Erase(LevelDrawBuffer * drawBuffer, Level * level) override;
};

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

    std::vector<Shrapnel> Explode(class Level * level) const;

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
