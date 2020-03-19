#include "base.h"
#include <cstdio>

#include <level.h>
#include <projectile.h>
#include <random.h>
#include <tank.h>
#include <tweak.h>

std::vector<Shrapnel> Explosion::Explode(Position pos, Level *level, int count, int radius, int ttl)
{
    /*  Beware. Explosions use multiplied positions to maintain 'floating' fraction as they move less than one pixel
     * between frames */
    auto items = std::vector<Shrapnel>{};
    items.reserve(count);
    /* Add all of the effect particles: */
    for (int i = 0; i < count; i++)
    {
        items.emplace_back(
            Shrapnel{Position{pos.x * 16 + 8, pos.y * 16 + 8},
                       SpeedF{float(Random.Int(0, radius) - radius / 2), float(Random.Int(0, radius) - radius / 2)},
                       Random.Int(0, ttl), level});
    }
    return items;
}