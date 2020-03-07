#ifndef _TANK_H_
#define _TANK_H_
#include <memory>


typedef struct Tank Tank;

/* For the controllers/AIs: */
#include <levelslice.h>

/* Put inside a structure, so we are protected from casual AI cheating: */
typedef struct PublicTankInfo {
	int health, energy;
	int x, y; /* relative from home base */
	LevelSlice *slice;
} PublicTankInfo;

typedef void (*TankController)(PublicTankInfo *, void *, int *, int *, int *) ;


#include <level.h>
#include <screen.h>
#include <drawbuffer.h>
#include <projectile.h>

struct Tank
{
public:
	Position pos;
	Speed speed; /* Velocity... ie: is it moving now? */
	int direction;

	int color;

	int bullet_timer, bullets_left, is_shooting;

	int health, energy;

	TankController controller;
	std::shared_ptr<void> controller_data;

	Level* lvl;
	PList* pl;
	std::shared_ptr<LevelSlice> cached_slice;

	Tank(int color, Level* lvl, PList* pl, Position pos);
	~Tank() = default;

	[[nodiscard]] Position GetPosition() const { return this->pos; }
	[[nodiscard]] int GetColor() const { return this->color; }
	[[nodiscard]] int GetDirection() const { return this->direction; }
};

void tank_get_stats(Tank *t, int *energy, int *health) ;
void tank_move(Tank *t, struct TankList *tl) ;
void tank_try_base_heal(Tank *t) ;

void tank_clear(Tank *t, DrawBuffer *b) ;
void tank_draw(Tank *t, DrawBuffer *b) ;

void tank_return_bullet(Tank *t) ;
void tank_alter_energy(Tank *t, int diff) ;
void tank_alter_health(Tank *t, int diff) ;
void tank_trigger_explosion(Tank *t) ;

void tank_set_controller(Tank *t, TankController func, std::shared_ptr<void> data) ;

int tank_is_dead(Tank *t) ;

#endif /* _TANK_H_ */

