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


Tank* TankList::AddTank(int color, Vector p)
{
	auto found= std::find_if(begin(), end(), [color](auto& el) { return color == el.color; });
	if (found != list.end())
		throw GameException("already exists");
	
	this->list.emplace_back(std::make_unique<Tank>(color, this->lvl, this->pl, p.x, p.y));
	return this->list.back().get();
}

void TankList::RemoveTank(int color)
{
	auto count = list.size();
	// Delete all with this id
	list.erase(std::remove_if(begin(), end(), [color](auto& el) { return color == el.color; }), list.end());

	if (list.size() == count)
		throw GameException("this id doesn't exist");
	if (list.size() != count - 1)
		throw GameException("multiple entries exist");
}

Tank* TankList::GetTankAtPoint(int x, int y, int ignored) {
	
	for(Tank& tank : *this) 
	{
		if(tank.color == ignored || tank_is_dead(&tank)) continue;
		
		int tx, ty;
		tank_get_position(&tank, &tx, &ty);
		tx = x - tx + 3; ty = y - ty + 3;
		if(tx < 0 || tx > 6 || ty < 0 || ty > 6) continue;
		
		if(TANK_SPRITE[ tank_get_dir(&tank) ][ty][tx])
			return &tank;
	}
	return nullptr;
}

/* Note: change that vector to two int's eventually... */
bool TankList::CheckForCollision(Tank& tank, Position atPos, int atDirection)
{
	for (Tank& otherTank : *this) 
	{
		int x, y, lx, ly, ux, uy;
		
		if(otherTank.color == tank.color || tank_is_dead(&otherTank) ) continue;
		
		/* Let's see if these two tanks are ANYWHERE near each other: */
		tank_get_position(&otherTank, &x, &y);
		if(abs(atPos.x - x)>6 || abs(atPos.y - y)>6) continue;
		
		/* Ok, if we're here, the two tanks are real close. Now it's time for
		 * brute-force pixel checking: */
		int dir = tank_get_dir(&otherTank);
		
		/* Find the bounds of the two sprite's overlap: */
		if(x< atPos.x) { lx= atPos.x-3; ux=x+3;   }
		else	       { lx=x-3;		ux= atPos.x+3; }
		if(y< atPos.y) { ly= atPos.y-3; uy=y+3;   }
		else		   { ly=y-3;		uy= atPos.y+3; }
		
		/* Check the overlap for collisions: */
		for(int ty = ly; ty<=uy; ty++)
			for(int tx = lx; tx<=ux; tx++)
				if(TANK_SPRITE[dir] [ty-y+3] [tx-x+3] &&
				   TANK_SPRITE[atDirection][ty- atPos.y+3][tx- atPos.x+3])
					return true;
	}
	
	return false;
}