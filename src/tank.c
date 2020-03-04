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


Tank::Tank(Level *lvl, PList *pl, unsigned x, unsigned y, unsigned color) :
	x(x), y(y), color(color)
{
	this->cached_slice = level_slice_new(lvl, this);
	
	/* Let's just make the starting direction random, because we can: */
	this->direction = rand_int(0u, 7u);
	if(this->direction >= 4) this->direction ++;
	
	this->vx = this->vy = 0; this->lvl = lvl; this->pl = pl;
	 
	this->bullet_timer = TANK_BULLET_DELAY;
	this->bullets_left = TANK_BULLET_MAX;
	this->is_shooting = 0;
	this->health = TANK_STARTING_SHIELD;
	this->energy = TANK_STARTING_FUEL;
	this->controller = NULL;
	this->controller_data = NULL;
}


Tank::~Tank()
{
	level_slice_free(this->cached_slice);
	free_mem(this->controller_data);
}

void tank_get_position(Tank *t, unsigned *x, unsigned *y) {
	*x = t->x; *y = t->y;
}

void tank_get_stats(Tank *t, int *energy, int *health) {
	*energy = t->energy; *health = t->health;
}

unsigned tank_get_dir(Tank *t) {
	return t->direction;
}

unsigned Tank::get_color() const {
	return this->color;
}

/* We don't use the Tank structure in this function, since we are checking the
 * tank's hypothetical position... ie: IF we were here, would we collide? */

typedef enum CollisionType {
	CT_NONE,    /* All's clear! */
	CT_DIRT,    /* We hit dirt, but that's it. */
	CT_COLLIDE  /* Hit a rock/base/tank/something we can't drive over. */
} CollisionType;

static CollisionType tank_collision(Level *lvl, unsigned dir, unsigned x, unsigned y, TankList *tl, unsigned id) {
	int tx, ty;
	CollisionType out = CT_NONE;
	
	/* Level Collisions: */
	for(ty=-3; ty<=3; ty++)
		for(tx=-3; tx<=3; tx++) {
			char c = TANK_SPRITE[dir][3+ty][3+tx];
			if(!c) continue;
			
			c = level_get(lvl, x+tx, y+ty);
			
			if(c==DIRT_HI || c==DIRT_LO) out = CT_DIRT;
			
			if(c!=DIRT_HI && c!=DIRT_LO && c!=BLANK) return CT_COLLIDE;
		}
	
	/* Tank collisions: */
	if(tanklist_check_collision(tl, Vector{x,y}, dir, id)) return CT_COLLIDE;
	return out;
}

void tank_move(Tank *t, TankList *tl) {
	
	
	/* Don't let this tank do anything if it is dead: */
	if(!t->health) return;
	
	/* Calculate all of our motion: */
	if(t->controller) {
		Vector         base = level_get_spawn(t->lvl, t->color);
		PublicTankInfo i = {
			.health = t->health,
			.energy = t->energy,
			.x      = static_cast<int>(t->x - base.x),
			.y      = static_cast<int>(t->y - base.y),
			.slice  = t->cached_slice};
		t->controller(&i, t->controller_data, &t->vx, &t->vy, &t->is_shooting);
	}
	
	/* Calculate the direction: */
	if(t->vx != 0 || t->vy != 0) {
		CollisionType ct;
		
		unsigned newdir = static_cast<unsigned> ((t->vx+1) + (t->vy+1) * 3);
		
		ct = tank_collision(t->lvl, newdir, t->x+t->vx, t->y+t->vy, tl, t->color);
		/* Now, is there room to move forward in that direction? */
		if( ct != CT_COLLIDE ) {
			
			/* If so, then we can move: */
			if( ct == CT_DIRT ) {
				level_dig_hole(t->lvl, t->x+t->vx, t->y+t->vy);
				if(t->is_shooting) goto shooting_speedup;
			
			} else {
				
shooting_speedup:				
				/* We will only move/rotate if we were able to get here without
				 * digging, so we can avoid certain bizarre bugs: */
				t->direction = newdir;
				t->x += t->vx; t->y += t->vy;

				/* Well, we moved, so let's charge ourselves: */
				tank_alter_energy(t, TANK_MOVE_COST);
			}
		}
	}
	
	/* Handle all shooting logic: */
	if(t->bullet_timer == 0) {
		if(t->is_shooting && t->bullets_left > 0) {
			
			plist_push_bullet(t->pl, t);

			/* We just fired. Let's charge ourselves: */
			tank_alter_energy(t, TANK_SHOOT_COST);
			
			t->bullets_left --;
			t->bullet_timer = TANK_BULLET_DELAY;
		}
	} else t->bullet_timer--;
}

/* Check to see if we're in any bases, and heal based on that: */
void tank_try_base_heal(Tank *t) {
	BaseCollision c;

	c = level_check_base_collision(t->lvl, t->x, t->y, t->color);
	if(c == BASE_COLLISION_YOURS) {
		tank_alter_energy(t, TANK_HOME_CHARGE);
		tank_alter_health(t, TANK_HOME_HEAL);

	} else if(c == BASE_COLLISION_ENEMY)
		tank_alter_energy(t, TANK_ENEMY_CHARGE);
}

void tank_draw(Tank *t, DrawBuffer *b) {
	unsigned x, y;
	
	if(!t->health) return;
	
	for(y=0; y<7; y++)
		for(x=0; x<7; x++) {
			char val = TANK_SPRITE[t->direction][y][x];
			if(val)
				drawbuffer_set_pixel(b, t->x + x-3, t->y + y-3, color_tank[t->color][val-1]);
		}
}

void tank_clear(Tank *t, DrawBuffer *b) {
	unsigned x, y;
	
	if(!t->health) return;
	
	for(y=0; y<7; y++)
		for(x=0; x<7; x++)
			if(TANK_SPRITE[t->direction][y][x])
				drawbuffer_set_pixel(b, t->x + x-3, t->y + y-3, color_blank);
}

void tank_return_bullet(Tank *t) {
	t->bullets_left ++;
}

void tank_alter_energy(Tank *t, int diff) {

	/* You can't alter energy if the tank is dead: */
	if(tank_is_dead(t)) return;
	
	/* If the diff would make the energy negative, then we just set it to 0: */
	if(diff < 0 && -diff >= t->energy) {
		t->energy = 0;
		tank_alter_health(t, -TANK_STARTING_SHIELD);
		return;
	}

	/* Else, just add, and account for overflow: */
	t->energy += diff;
	if(t->energy > TANK_STARTING_FUEL) t->energy = TANK_STARTING_FUEL;
}

void tank_alter_health(Tank *t, int diff) {

	/* Make sure we don't come back from the dead: */
	if(tank_is_dead(t)) return;
	
	if(diff < 0 && -diff >= t->health) {
		t->health = 0;
		t->energy = 0;
		tank_trigger_explosion(t);
		return;
			
	}

	t->health += diff;
	if(t->health > TANK_STARTING_SHIELD) t->health = TANK_STARTING_SHIELD;
}

void tank_trigger_explosion(Tank *t) {
	plist_push_explosion(t->pl,
		t->x, t->y,
		EXPLOSION_DEATH_COUNT,
		EXPLOSION_DEATH_RADIUS,
		EXPLOSION_DEATH_TTL
	);
}

/* This is meant to be called from a controller's attach function: */
void tank_set_controller(Tank *t, TankController func, void *data) {
	t->controller = func;
	
	if(t->controller_data) free_mem(t->controller_data);
	t->controller_data = data;
}

int tank_is_dead(Tank *t) {
	return !t->health;
}