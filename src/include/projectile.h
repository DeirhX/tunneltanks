#pragma once

#include <containers.h>
#include "types.h"
#include <vector>

enum class ProjectileType {
	Invalid,
	Bullet,
	Explosion,
};

class VoxelRaycast
{
	
};

struct Projectile {
	Position pos;       /* The x,y of the 'hot' portion.  (#ff3408) */
	Position pos_old;   /* The x,y of the 'cold' portion. (#ba0000) */

	//PositionF 
	Speed    speed;
	int      steps_remain;

	ProjectileType type;
	bool     is_alive = false;

	class Level* level;
	class Tank* tank;

private:
	Projectile() = default; // Never use manually. Will be used inside intrusive containers
public:
	static Projectile Invalid() { return Projectile(); }
public:
    Projectile(Position position, Position origin, SpeedF speed, int life, ProjectileType type, Level* level, Tank* tank);

	static std::vector<Projectile> CreateExplosion(Position pos, Level* level, int count, int radius, int ttl);
	static Projectile CreateBullet(Tank* tank);

	bool IsInvalid() const { return !is_alive; }
	bool IsValid() const { return is_alive; }
	void Invalidate() { is_alive = false; }
};

//class Bullet : public Projectile {};