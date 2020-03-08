#pragma once

#include <containers.h>
#include "types.h"
#include <vector>


struct Projectile;
struct PList;
struct PListNode;

/*
PList* plist_new();
void plist_destroy(PList* pl);

void plist_push_bullet(PList* pl, struct Tank* t);
void plist_push_explosion(PList* pl, int x, int y, int count, int r, int ttl);

void plist_step(PList* pl, struct Level* b, struct TankList* tl);

void plist_clear(PList* pl, struct DrawBuffer* b);
void plist_draw(PList* pl, struct DrawBuffer* b);
*/

struct Projectile {
	Position pos;       /* The x,y of the 'hot' portion.  (#ff3408) */
	Position pos_old;   /* The x,y of the 'cold' portion. (#ba0000) */

	Speed    step;
	int		 life;

	bool	is_effect;
	bool	is_alive;

	struct Tank* tank;

public:
	Projectile() = default;
    Projectile(Position position, Position origin, Speed speed, int life, bool is_effect, Tank* tank);

	static std::vector<Projectile> CreateExplosion(Position pos, int count, int r, int ttl);
	static Projectile CreateBullet(Tank* t);

	bool IsInvalid() { return !is_alive; }
	bool IsValid() { return is_alive; }
	void Invalidate() { is_alive = false; }
};


class ProjectileList
{
    ValueContainer<Projectile> container;
public:
	Projectile& Add(Projectile projectile = {});
	void Add(std::vector<Projectile> projectiles);
	void Remove(Projectile& projectile) { projectile.Invalidate(); }
	void Shrink() { container.Shrink(); }

	void Advance(struct Level* level, struct TankList* tankList);
	void Erase(struct DrawBuffer* drawBuffer);
    void Draw(struct DrawBuffer* drawBuffer);
};