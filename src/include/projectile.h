#pragma once

#include <containers.h>
#include "types.h"
#include <vector>

enum class ProjectileType {
	Invalid,
	Bullet,
	Explosion,
	Shrapnel,
};

class VoxelRaycast
{

};

struct Projectile {
    Position pos;       /* The x,y of the 'hot' portion.  (#ff3408) */

	//PositionF 
	Speed    speed;
	int      steps_remain;

	bool	is_alive = false;

	class Level* level;

private:
	Projectile() = default; // Never use manually. Will be used inside intrusive containers
protected:
	Projectile(Position position, SpeedF speed, int life, Level* level)
    : pos(position), speed(int(speed.x), int(speed.y)), steps_remain(life), level(level), is_alive(true)
	{ }
public:
    virtual ~Projectile() = default;
	virtual ProjectileType GetType() = 0;

	bool IsInvalid() const { return !is_alive; }
	bool IsValid() const { return is_alive; }
	void Invalidate() { is_alive = false; }
};

class MotionBlurProjectile : public Projectile
{
public:
    Position pos_blur_from; /* The x,y of the 'cold' portion. (#ba0000) */
protected:
    MotionBlurProjectile(Position position, SpeedF speed, int life, Level * level)
    : Projectile(position, speed, life, level) { }
};

class Bullet : public MotionBlurProjectile
{
	using Base = MotionBlurProjectile;
  public:
    class Tank *tank;
  public:
    Bullet() = default;
    Bullet(Position position, SpeedF speed, int life, Level *level, Tank *tank)
        : Base(position, speed, life, level), tank(tank) { }
    ProjectileType GetType() override { return ProjectileType::Bullet; }
};

class Shrapnel : public Projectile
{
  public:
    Shrapnel() = default;
    Shrapnel(Position position, SpeedF speed, int life, Level *level)
        : Projectile(position, speed, life, level) { }
    ProjectileType GetType() override { return ProjectileType::Shrapnel; }
};

class Explosion : public Projectile
{
public:
    static std::vector<Shrapnel> Explode(Position pos, Level *level, int count, int radius, int ttl);
};