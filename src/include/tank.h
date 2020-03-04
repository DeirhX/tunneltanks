#ifndef _TANK_H_
#define _TANK_H_


typedef struct Tank Tank;

/* For the controllers/AIs: */
#include <levelslice.h>

/* Put inside a structure, so we are protected from casual AI cheating: */
typedef struct PublicTankInfo {
	int health, energy;
	int x, y; /* relative from home base */
	LevelSlice *slice;
} PublicTankInfo;

typedef void (*TankController)(PublicTankInfo *, void *, int *, int *, unsigned *) ;


#include <level.h>
#include <screen.h>
#include <drawbuffer.h>
#include <projectile.h>

struct Tank
{
public:
	unsigned x, y;
	int vx, vy; /* Velocity... ie: is it moving now? */
	unsigned direction;

	unsigned color;

	unsigned bullet_timer, bullets_left, is_shooting;

	int health, energy;

	TankController controller;
	void* controller_data;

	Level* lvl;
	PList* pl;
	LevelSlice* cached_slice;

	Tank(Level* lvl, PList* pl, unsigned x, unsigned y, unsigned color);
	~Tank();

	unsigned get_color() const;
};

unsigned tank_get_dir(Tank *t) ;
void tank_get_stats(Tank *t, int *energy, int *health) ;
void tank_get_position(Tank *t, int *x, int *y) ;
void tank_move(Tank *t, struct TankList *tl) ;
void tank_try_base_heal(Tank *t) ;

void tank_clear(Tank *t, DrawBuffer *b) ;
void tank_draw(Tank *t, DrawBuffer *b) ;

void tank_return_bullet(Tank *t) ;
void tank_alter_energy(Tank *t, int diff) ;
void tank_alter_health(Tank *t, int diff) ;
void tank_trigger_explosion(Tank *t) ;

void tank_set_controller(Tank *t, TankController func, void *data) ;

int tank_is_dead(Tank *t) ;

#endif /* _TANK_H_ */

