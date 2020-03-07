#pragma once
#include <memory>
/* For the controllers/AIs: */
//#include <levelslice.h>
#include <level.h>
#include <screen.h>
#include <drawbuffer.h>
#include <projectile.h>

struct LevelSlice;

/* Put inside a structure, so we are protected from casual AI cheating: */
typedef struct PublicTankInfo {
	int health, energy;
	int x, y; /* relative from home base */
	LevelSlice *slice;
} PublicTankInfo;

typedef void (*TankController)(PublicTankInfo *, void *, int *, int *, int *) ;

struct Tank
{
public:
	bool is_valid = true;
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
	//~Tank() = default;
	void SetController(TankController func, std::shared_ptr<void> data);

	[[nodiscard]] Position GetPosition() const { return this->pos; }
	[[nodiscard]] int GetColor() const { return this->color; }
	[[nodiscard]] int GetDirection() const { return this->direction; }

	[[nodiscard]] bool IsDead() const;
	[[nodiscard]] bool IsInvalid() const { return !this->is_valid; } // For ValueContainer
	[[nodiscard]] int GetEnergy() const { return this->energy; }
	[[nodiscard]] int GetHealth() const { return this->health; }
	void Invalidate() { this->is_valid = false; }
    void AlterEnergy(int diff);
	void AlterHealth(int diff);

    void DoMove(struct TankList* tl);
	void TryBaseHeal();

	void Clear(DrawBuffer* b) const;
	void Draw(DrawBuffer* b) const;

	void ReturnBullet();
	void TriggerExplosion() const;
};





