#include "base.h"
#include <cstdio>

#include <projectile.h>
#include <level.h>
#include <random.h>
#include <tank.h>
#include <tweak.h>


Projectile::Projectile(Position position, Position origin, SpeedF speed, int life, ProjectileType type, Level* level, Tank* tank)
    : pos_to(position), pos_from(origin), speed(int(speed.x), int(speed.y)), steps_remain(life), type(type), level(level), tank(tank), is_alive(true)
{

}

std::vector<Projectile> Projectile::CreateExplosion(Position pos, Level* level, int count, int radius, int ttl)
{
	/*  Beware. Explosions use multiplied positions to maintain 'floating' fraction as they move less than one pixel between frames */
	auto items = std::vector<Projectile>{};
	items.reserve(count);
	/* Add all of the effect particles: */
	for (int i = 0; i < count; i++) {
		items.emplace_back(Projectile{
			Position{pos.x * 16 + 8, pos.y * 16 + 8},
			Position{pos.x, pos.y},
			SpeedF { float(Random.Int(0,radius) - radius / 2), float(Random.Int(0,radius) - radius / 2)},
			Random.Int(0,ttl), ProjectileType::Explosion, level, nullptr });
	}
	return items;
}

Projectile Projectile::CreateBullet(Tank* tank)
{
	return Projectile {
	    tank->GetPosition(),
	    tank->GetPosition(),
		tank->GetDirection(), tweak::tank::BulletSpeed,
	    ProjectileType::Bullet, tank->GetLevel(), tank };
}
