#include "projectile.h"
#include "projectile_list.h"
#include "level.h"
#include "random.h"
#include "colors.h"
#include "tank.h"
#include "tanklist.h"

Projectile& ProjectileList::Add(Projectile projectile)
{
	return this->container.Add(projectile);
}

void ProjectileList::Add(std::vector<Projectile> projectiles)
{
	for (auto& projectile : projectiles)
		this->container.Add(projectile);
}

void ProjectileList::Advance(Level* level, TankList* tankList)
{
	auto newly_added = std::vector<Projectile>{};
	/* Iterate over the living objects: */
	for (Projectile& p : container)
	{
		/* Is this a bullet, or an effect? */
		if (p.type == ProjectileType::Explosion) {
			/* Effect: */

			/* Did this expire? */
			if (!p.life) {
				Remove(p);
				continue;
			}

			/* Move the effect: */
			p.life--;
			p.pos.x += p.step.x; p.pos.y += p.step.y;
			Position pos = { p.pos.x / 16, p.pos.y / 16 };

			/* Make sure we didn't hit a level detail: */
			LevelVoxel c = level->GetVoxel(pos);
			if (Voxels::IsBlockingCollision(c)) {
				Remove(p);
				continue;
			}

			/* Effects blank everything out in their paths: */
			level->SetVoxel(pos, Random.Bool(500) ? LevelVoxel::DecalHigh : LevelVoxel::DecalLow);

		}
		else {
			int i;

			/* Bullet: */

			for (i = 0; i < p.life; i++) {
				p.pos_old.x = p.pos.x;     p.pos_old.y = p.pos.y;
				p.pos.x += p.step.x;		 p.pos.y += p.step.y;

				/* Did we hit another tank? */
				int hitTankColor = p.tank->GetColor();
				Tank* hitTank = tankList->GetTankAtPoint(p.pos.x, p.pos.y, hitTankColor);
				if (hitTank) {
					/* If we have an associated tank, return the shot: */
					p.tank->ReturnBullet();

					/* Hurt the tank we hit: */
					hitTank->AlterHealth(TANK_SHOT_DAMAGE);

					/* Add all of the effect particles: */

					newly_added.reserve(newly_added.size() + EXPLOSION_HURT_COUNT);
					for (Projectile& proj : Projectile::CreateExplosion(Position{ p.pos_old.x, p.pos_old.y }, level,
						EXPLOSION_HURT_COUNT,
						EXPLOSION_HURT_RADIUS,
						EXPLOSION_HURT_TTL))
					{
						newly_added.emplace_back(proj);
					}

					/* Finally, remove it: */
					Remove(p);
					break;
				}

				/* Else, did we hit something in the level? */
				LevelVoxel c = level->GetVoxel(p.pos);
				if (Voxels::IsAnyCollision(c)) {
					/* If we have an associated tank, return the shot: */
					p.tank->ReturnBullet();

					/* Add all of the effect particles: */
					newly_added.reserve(newly_added.size() + EXPLOSION_DIRT_COUNT);
					for (Projectile& proj : Projectile::CreateExplosion(Position{ p.pos_old.x, p.pos_old.y }, level,
						EXPLOSION_DIRT_COUNT,
						EXPLOSION_DIRT_RADIUS,
						EXPLOSION_DIRT_TTL))
					{
						newly_added.emplace_back(proj);
					}

					/* Finally, remove it: */
					Remove(p);
					break;
				}
			}
		}
	}

	Add(newly_added);
	Shrink();
}

void ProjectileList::Erase(DrawBuffer* drawBuffer, Level* level)
{
	for (Projectile& p : container)
	{
		if (p.type == ProjectileType::Explosion)
			level->CommitPixel(Position{ p.pos.x / 16, p.pos.y / 16 });
		else {
			level->CommitPixel(Position{ p.pos.x, p.pos.y });
			level->CommitPixel(Position{ p.pos_old.x, p.pos_old.y });
		}
	}
}


void ProjectileList::Draw(DrawBuffer* drawBuffer)
{
	for (Projectile& p : container)
	{
		if (p.type == ProjectileType::Explosion)
			drawBuffer->SetPixel(Position{ p.pos.x / 16, p.pos.y / 16 }, Palette.Get(Colors::FireHot));
		else {
			drawBuffer->SetPixel(Position{ p.pos.x, p.pos.y }, Palette.Get(Colors::FireHot));
			drawBuffer->SetPixel(Position{ p.pos_old.x, p.pos_old.y }, Palette.Get(Colors::FireCold));
		}
	}
}
