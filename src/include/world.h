#include "tanklist.h"
#include "projectile_list.h"

class World
{
public:
	std::unique_ptr<TankList> tank_list;
	std::unique_ptr<ProjectileList> projectiles;
public:
	void Advance(class Level* level, class DrawBuffer* drawBuffer);
};

