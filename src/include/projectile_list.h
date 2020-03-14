#pragma once
#include "containers.h"
#include "projectile.h"

class ProjectileList
{
	ValueContainer<Projectile> container;
public:
	Projectile& Add(Projectile projectile);
	void Add(std::vector<Projectile> projectiles);
	void Remove(Projectile& projectile) { projectile.Invalidate(); }
	void Shrink() { container.Shrink(); }

	void Advance(class Level* level, class TankList* tankList);
	void Erase(class DrawBuffer* drawBuffer, class Level* level);
	void Draw(class DrawBuffer* drawBuffer);
};
