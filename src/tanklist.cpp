#include "base.h"
#include <cstdlib>

#include <tanklist.h>
#include <tank.h>
#include <types.h>
#include <level.h>
#include <projectile.h>
#include <tweak.h>
#include <tanksprites.h>
#include <ranges>

#include "exceptions.h"
#include <algorithm>
#include "projectile_list.h"

TankList::TankList(Level *lvl, ProjectileList *pl): lvl(lvl), pl(pl)
{
}

TankList::~TankList()
{
}


Tank* TankList::AddTank(TankColor color, Vector p)
{
	auto found= std::find_if(begin(), end(), [color](auto& el) { return color == el.color; });
	if (found != list.end())
		throw GameException("already exists");

	return &this->list.ConstructElement(color, this->lvl, this->pl, Position{p.x, p.y});
	//return this->list.Add(std::make_unique<Tank>(color, this->lvl, this->pl, Position{p.x, p.y}));
}

void TankList::RemoveTank(TankColor color)
{
	auto found = std::find_if(begin(), end(), [color](auto& el) { return color == el.color; });
	(*found).Invalidate();
}

Tank* TankList::GetTankAtPoint(int x, int y, int ignored) {
	
	for(Tank& tank : *this) 
	{
		if(tank.color == ignored || tank.IsDead()) continue;
		
		Position pos = tank.GetPosition();
		pos.x = x - pos.x + 3; pos.y = y - pos.y + 3;
		if(pos.x < 0 || pos.x > 6 || pos.y < 0 || pos.y > 6) continue;
		
		if(TANK_SPRITE[ tank.GetDirection() ][pos.y][pos.x])
			return &tank;
	}
	return nullptr;
}

/* Note: change that vector to two int's eventually... */
bool TankList::CheckForCollision(Tank& tank, Position testPos, int testDirection)
{
	for (Tank& otherTank : *this) 
	{
		int lx, ly, ux, uy;
		
		if(otherTank.color == tank.color || otherTank.IsDead()) continue;
		
		/* Let's see if these two tanks are ANYWHERE near each other: */
		Position pos = otherTank.GetPosition();
		if(abs(testPos.x - pos.x)>6 || abs(testPos.y - pos.y)>6) continue;
		
		/* Ok, if we're here, the two tanks are real close. Now it's time for
		 * brute-force pixel checking: */
		int dir = otherTank.GetDirection();
		
		/* Find the bounds of the two sprite's overlap: */
		if(pos.x< testPos.x) { lx= testPos.x-3; ux= pos.x+3;   }
		else	       { lx= pos.x-3;		ux= testPos.x+3; }
		if(pos.y< testPos.y) { ly= testPos.y-3; uy= pos.y+3;   }
		else		   { ly= pos.y-3;		uy= testPos.y+3; }
		
		/* Check the overlap for collisions: */
		for(int ty = ly; ty<=uy; ty++)
			for(int tx = lx; tx<=ux; tx++)
				if(TANK_SPRITE[dir] [ty- pos.y+3] [tx- pos.x+3] &&
				   TANK_SPRITE[testDirection][ty- testPos.y+3][tx- testPos.x+3])
					return true;
	}
	
	return false;
}