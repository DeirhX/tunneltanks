#include <cstdlib>

#include <tanklist.h>
#include <tank.h>
#include <types.h>
#include <memalloc.h>
#include <level.h>
#include <projectile.h>
#include <tweak.h>
#include <tanksprites.h>
#include <vector>
#include <cassert>
#include <ranges>

#include "exceptions.h"
#include <algorithm>

TankList::TankList(Level *lvl, PList *pl): lvl(lvl), pl(pl)
{
}

TankList::~TankList()
{
}


Tank* TankList::AddTank(int id, Vector p)
{
	auto found= std::find_if(begin(), end(), [id](auto& el) { return id == el.id; });
	if (found != list.end())
		throw GameException("already exists");
	
	this->list.emplace_back(std::make_unique<Tank>(id, this->lvl, this->pl, p.x, p.y, id));
	return this->list.back().get();
}

void TankList::RemoveTank(int id)
{
	auto count = list.size();
	// Delete all with this id
	list.erase(std::remove_if(begin(), end(), [id](auto& el) { return id == el.id; }), list.end());

	if (list.size() == count)
		throw GameException("this id doesn't exist");
	if (list.size() != count - 1)
		throw GameException("multiple entries exist");
}

Tank *tanklist_check_point(TankList *tl, int x, int y, int ignored) {
	
	for(Tank& tank : *tl) {
		int tx, ty;
		if(tank.id == ignored || tank_is_dead(&tank)) continue;
		
		tank_get_position(&tank, &tx, &ty);
		tx = x - tx + 3; ty = y - ty + 3;
		if(tx < 0 || tx > 6 || ty < 0 || ty > 6) continue;
		
		if(TANK_SPRITE[ tank_get_dir(&tank) ][ty][tx])
			return &tank;
	}
	return NULL;
}

/* Note: change that vector to two int's eventually... */
int tanklist_check_collision(TankList *tl, Vector p, int pdir, int ignored) {
	
	for (Tank& tank : *tl) {
		int x, y, dir, tx, ty, lx, ly, ux, uy;
		
		if( tank.id == ignored || tank_is_dead(&tank) ) continue;
		
		/* Let's see if these two tanks are ANYWHERE near each other: */
		tank_get_position(&tank, &x, &y);
		if(abs(p.x-x)>6 || abs(p.y-y)>6) continue;
		
		/* Ok, if we're here, the two tanks are real close. Now it's time for
		 * brute-force pixel checking: */
		dir = tank_get_dir(&tank);
		
		/* Find the bounds of the two sprite's overlap: */
		if(x<p.x) { lx=p.x-3; ux=x+3;   }
		else      { lx=x-3;   ux=p.x+3; }
		if(y<p.y) { ly=p.y-3; uy=y+3;   }
		else      { ly=y-3;   uy=p.y+3; }
		
		/* Check the overlap for collisions: */
		for(ty=ly; ty<=uy; ty++)
			for(tx=lx; tx<=ux; tx++)
				if(TANK_SPRITE[dir] [ty-y+3]  [tx-x+3] &&
				   TANK_SPRITE[pdir][ty-p.y+3][tx-p.x+3])
					return 1;
	}
	
	return 0;
}