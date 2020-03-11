#pragma once
#include <memory>
/* For the controllers/AIs: */
//#include <levelslice.h>
#include <level.h>
#include <screen.h>
#include <drawbuffer.h>
#include <projectile.h>
#include <LevelView.h>

struct LevelView;

/* Put inside a structure, so we are protected from casual AI cheating: */
typedef struct PublicTankInfo {
	int health, energy;
	int x, y; /* relative from home base */
	LevelView level_view;
} PublicTankInfo;

typedef void (*TankController)(PublicTankInfo *, void *, Speed *, int *) ;

struct Tank
{
public:
	bool is_valid = false;
	Position pos;
	Speed speed; /* Velocity... ie: is it moving now? */
	int direction;

	TankColor color;

	int bullet_timer, bullets_left, is_shooting;

	int health, energy;

	TankController controller;
	std::shared_ptr<void> controller_data;

	Level* lvl;
	ProjectileList* pl;
	//std::shared_ptr<LevelView> cached_slice;

private:
	Tank() = default; // Never use manually. Will be used inside intrusive containers
public:
	static Tank Invalid() { return Tank(); }

	Tank(TankColor color, Level* lvl, ProjectileList* pl, Position pos);
	//~Tank() = default;
	void SetController(TankController func, std::shared_ptr<void> data);

	[[nodiscard]] Position GetPosition() const { return this->pos; }
	[[nodiscard]] int GetColor() const { return this->color; }
	[[nodiscard]] int GetDirection() const { return this->direction; }

	[[nodiscard]] bool IsDead() const;
	[[nodiscard]] bool IsValid() const { return this->is_valid; } // For ValueContainer
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





