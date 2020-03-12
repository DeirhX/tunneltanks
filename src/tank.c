#include <cstdlib>

#include <tank.h>
#include <level.h>
#include <memalloc.h>
#include <screen.h>
#include <tweak.h>
#include <tanksprites.h>
#include <drawbuffer.h>
#include <projectile.h>
#include <random.h>
#include <tanklist.h>
#include <level_view.h>
#include <algorithm>


Tank::Tank(TankColor color, Level *lvl, ProjectileList*pl, Position pos) :
	pos(pos), color(color), is_valid(true)
{
	// this->cached_slice = std::make_shared<LevelView>(this, lvl);
	
	/* Let's just make the starting direction random, because we can: */
	this->direction = Random.Int(0, 7);
	if(this->direction >= 4) this->direction ++;
	
	this->lvl = lvl;
    this->pl = pl;
	 
	this->bullet_timer = TANK_BULLET_DELAY;
	this->bullets_left = TANK_BULLET_MAX;
	this->is_shooting = 0;
	this->health = TANK_STARTING_SHIELD;
	this->energy = TANK_STARTING_FUEL;
}

/* We don't use the Tank structure in this function, since we are checking the
 * tank's hypothetical position... ie: IF we were here, would we collide? */

typedef enum CollisionType {
	CT_NONE,    /* All's clear! */
	CT_DIRT,    /* We hit dirt, but that's it. */
	CT_COLLIDE  /* Hit a rock/base/tank/something we can't drive over. */
} CollisionType;

static CollisionType tank_collision(Level *lvl, int dir, int x, int y, TankList *tl, Tank& tank) {
	int tx, ty;
	CollisionType out = CT_NONE;
	
	/* Level Collisions: */
	for(ty=-3; ty<=3; ty++)
		for(tx=-3; tx<=3; tx++) {
			char c = TANK_SPRITE[dir][3+ty][3+tx];
			if(!c) continue;
			
			c = lvl->GetVoxel({ x + tx, y + ty });
			
			if(c==DIRT_HI || c==DIRT_LO) out = CT_DIRT;
			
			if(c!=DIRT_HI && c!=DIRT_LO && c!=BLANK) return CT_COLLIDE;
		}
	
	/* Tank collisions: */
	if(tl->CheckForCollision(tank, Position{x,y}, dir)) return CT_COLLIDE;
	return out;
}

void Tank::DoMove(TankList *tl) {
	
	/* Don't let this tank do anything if it is dead: */
	if(!this->health) return;
	
	/* Calculate all of our motion: */
	if(this->controller) {
		Vector         base = this->lvl->GetSpawn(this->color);
		PublicTankInfo i = {
			.health = this->health,
			.energy = this->energy,
			.x      = static_cast<int>(this->pos.x - base.x),
			.y      = static_cast<int>(this->pos.y - base.y),
			.level_view  = LevelView(this, this->lvl)};
		
		this->ApplyControllerOutput(this->controller->ApplyControls(&i));
	}
	
	/* Calculate the direction: */
	if(this->speed.x != 0 || this->speed.y != 0) {
		CollisionType ct;
		
		int newdir = static_cast<int> ((this->speed.x+1) + (this->speed.y+1) * 3);
		
		ct = tank_collision(this->lvl, newdir, this->pos.x+this->speed.x, this->pos.y+this->speed.y, tl, *this);
		/* Now, is there room to move forward in that direction? */
		if( ct != CT_COLLIDE ) {
			
			/* If so, then we can move: */
			if( ct == CT_DIRT ) {
				this->lvl->DigHole(this->pos + (1 * this->speed));
			}
			if (ct != CT_DIRT || this->is_shooting)
            {
				/* We will only move/rotate if we were able to get here without
				 * digging, so we can avoid certain bizarre bugs: */
				this->direction = newdir;
				this->pos.x += this->speed.x; this->pos.y += this->speed.y;

				/* Well, we moved, so let's charge ourselves: */
				this->AlterEnergy(TANK_MOVE_COST);
			}
		}
	}
	
	/* Handle all shooting logic: */
	if(this->bullet_timer == 0) {
		if(this->is_shooting && this->bullets_left > 0) {
			
			this->pl->Add(Projectile::CreateBullet(this));

			/* We just fired. Let's charge ourselves: */
			this->AlterEnergy(TANK_SHOOT_COST);
			
			this->bullets_left --;
			this->bullet_timer = TANK_BULLET_DELAY;
		}
	} else this->bullet_timer--;
}

/* Check to see if we're in any bases, and heal based on that: */
void Tank::TryBaseHeal()
{
	BaseCollision c = this->lvl->CheckBaseCollision({this->pos.x, this->pos.y}, this->color);
	if(c == BaseCollision::Yours) {
		this->AlterEnergy(TANK_HOME_CHARGE);
		this->AlterHealth(TANK_HOME_HEAL);
	} else if(c == BaseCollision::Enemy)
		this->AlterEnergy(TANK_ENEMY_CHARGE);
}

void Tank::Draw(DrawBuffer *b) const
{
	if(!this->health) return;
	
	for(int y=0; y<7; y++)
		for(int x=0; x<7; x++) {
			char val = TANK_SPRITE[this->direction][y][x];
			if(val)
				drawbuffer_set_pixel(b, this->pos.x + x-3, this->pos.y + y-3, color_tank[this->color][val-1]);
		}
}

void Tank::Clear(DrawBuffer *b) const
{
	if(!this->health) return;
	
	for(int y=0; y<7; y++)
		for(int x=0; x<7; x++)
			if(TANK_SPRITE[this->direction][y][x])
				drawbuffer_set_pixel(b, this->pos.x + x-3, this->pos.y + y-3, color_blank);
}

void Tank::ReturnBullet()
{
	this->bullets_left++;
}

void Tank::AlterEnergy(int diff) {

	/* You can't alter energy if the tank is dead: */
	if(this->IsDead()) return;
	
	/* If the diff would make the energy negative, then we just set it to 0: */
	if(diff < 0 && -diff >= this->energy) {
		this->energy = 0;
		this->AlterHealth(-TANK_STARTING_SHIELD);
		return;
	}

	/* Else, just add, and account for overflow: */
	this->energy= std::min(this->energy+ diff, TANK_STARTING_FUEL);
}

void Tank::AlterHealth(int diff) {

	/* Make sure we don't come back from the dead: */
	if(this->IsDead()) return;
	
	if(diff < 0 && -diff >= this->health) {
		this->health = 0;
		this->energy = 0;
		this->TriggerExplosion();
		return;
	}

	this->health = std::min(this->health + diff, TANK_STARTING_SHIELD);
}

void Tank::TriggerExplosion() const
{
	this->pl->Add(Projectile::CreateExplosion(
		this->pos,
		EXPLOSION_DEATH_COUNT,
		EXPLOSION_DEATH_RADIUS,
		EXPLOSION_DEATH_TTL
	));
}

void Tank::ApplyControllerOutput(ControllerOutput controls)
{
	this->speed = controls.speed;
	this->is_shooting = controls.is_shooting;
}

bool Tank::IsDead() const
{
	return this->health <= 0;
}
