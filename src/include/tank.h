#pragma once
#include <memory>
/* For the controllers/AIs: */
//#include <levelslice.h>
#include <level.h>
#include <screen.h>
#include <drawbuffer.h>
#include <projectile.h>
#include <level_view.h>
#include <controllersdl.h>

struct LevelView;

/* Put inside a structure, so we are protected from casual AI cheating: */
typedef struct PublicTankInfo {
	int health, energy;
	int x, y; /* relative from home base */
	LevelView level_view;
} PublicTankInfo;

struct Tank
{
public:
	bool is_valid = false;

	bool is_shooting;

	Position pos;
	Speed speed; /* Velocity... ie: is it moving now? */
	int direction;

	TankColor color;

	int bullet_timer, bullets_left;

	int health, energy;

	std::shared_ptr<Controller> controller;

	Level* lvl;
	ProjectileList* pl;

private:
	Tank() = default; // Never use manually. Will be used inside intrusive containers
public:
	static Tank Invalid() { return Tank(); }

	Tank(TankColor color, Level* lvl, ProjectileList* pl, Position pos);
	void SetController(std::shared_ptr<Controller> newController) { this->controller = newController; }

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
	void ApplyControllerOutput(ControllerOutput controls);
};





