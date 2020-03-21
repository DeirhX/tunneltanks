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
};

/* Projectile base class */
struct Projectile
{
    Position pos; 
    // PositionF
    Speed speed;
    int simulation_steps;
    bool is_alive = false;
    class Level * level;

  private:
    Projectile() = default; // Never use manually. Will be used inside intrusive containers
  protected:
    Projectile(Position position, SpeedF speed, int life, Level * level)
        : pos(position), speed(int(speed.x), int(speed.y)), simulation_steps(life), is_alive(true), level(level) {  }

  public:
    virtual ~Projectile() = default;
    virtual ProjectileType GetType() = 0;
    virtual void Advance(class TankList * tankList) = 0;
    virtual void Draw(class DrawBuffer * drawBuffer) = 0;
    virtual void Erase(DrawBuffer * drawBuffer, Level * level) = 0;

    bool IsInvalid() const { return !is_alive; }
    bool IsValid() const { return is_alive; }
    void Invalidate() { is_alive = false; }
};

/* Projectile that leaves a trail */
class MotionBlurProjectile : public Projectile
{
  public:
    Position pos_blur_from; /* The x,y of the 'cold' portion. (#ba0000) */
  protected:
    MotionBlurProjectile(Position position, SpeedF speed, int life, Level * level)
        : Projectile(position, speed, life, level) { }
};

/* Projectile shot by a tank */
class Bullet : public MotionBlurProjectile
{
    using Base = MotionBlurProjectile;

  public:
    class Tank * tank;

  public:
    Bullet() = default;
    Bullet(Position position, SpeedF speed, int life, Level * level, Tank * tank)
        : Base(position, speed, life, level), tank(tank) { }
    ProjectileType GetType() override { return ProjectileType::Bullet; }

    void Advance(class TankList * tankList) override;
    void Draw(class DrawBuffer * drawBuffer) override;
    void Erase(DrawBuffer * drawBuffer, Level * level) override;
};

/* Non-damaging, non-collidable projectile spawned by the environment */
class Shrapnel : public Projectile
{
  public:
    Shrapnel() = default;
    Shrapnel(Position position, SpeedF speed, int life, Level * level) : Projectile(position, speed, life, level) {}
    ProjectileType GetType() override { return ProjectileType::Shrapnel; }

    void Advance(class TankList * tankList) override;
    void Draw(class DrawBuffer * drawBuffer) override;
    void Erase(DrawBuffer * drawBuffer, Level * level) override;
};

/* Helper class for MaSs DeStRuCtIoN! */
class Explosion : public Projectile
{
  public:
    static std::vector<Shrapnel> Explode(Position pos, Level * level, int count, int radius, int ttl);
};

class VoxelRaycast
{
};
