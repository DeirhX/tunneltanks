#pragma once

#include "types.h"
#include <containers.h>
#include <vector>

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
    Shrapnel(Position position, SpeedF speed, int life, Level * level) : Projectile(position, speed, level), life(life) {}
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
    MotionBlurProjectile(Position position, SpeedF speed, Level * level)
        : Projectile(position, speed, level) { }
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
        : Base(position, speed, level), simulation_steps(simulation_steps), tank(tank) { }
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
    ConcreteSpray(Position position, SpeedF speed, Level * level, Tank * tank) : Base(position, speed, level), tank(tank)
    { }
    ProjectileType GetType() override { return ProjectileType::Concrete; }
    void Advance(TankList * tankList) override;
    void Draw(LevelDrawBuffer * drawBuffer) override;
    void Erase(LevelDrawBuffer * drawBuffer, Level * level) override;
};

/* Helper class for MaSs DeStRuCtIoN! */
class Explosion : public Projectile
{
  public:
    static std::vector<Shrapnel> Explode(Position pos, Level * level, int count, int speed, int ttl);
    static std::vector<Shrapnel> FanOut(Position pos, DirectionF direction, Level * level, int count, int speed, int ttl);
};

class VoxelRaycast
{
};
