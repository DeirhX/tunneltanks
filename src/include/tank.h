#pragma once
#include <memory>
/* For the controllers/AIs: */
//#include <levelslice.h>
#include <level.h>
#include <drawbuffer.h>
#include <level_view.h>
#include <controllersdl.h>
#include "projectile_list.h"

struct LevelView;

/* Put inside a structure, so we are protected from casual AI cheating: */
struct PublicTankInfo {
	int health, energy;
	int x, y; /* relative from home base */
	LevelView level_view;
};

enum class CollisionType {
	None,    /* All's clear! */
	Dirt,    /* We hit dirt, but that's it. */
	Blocked  /* Hit a rock/base/tank/something we can't drive over. */
};

class Tank
{
public:
	bool is_valid = false;

	bool is_shooting = false;

	Position pos;
	Speed speed; /* Velocity... ie: is it moving now? */
	int direction;

	TankColor color;

	int bullet_timer = tweak::tank::BulletDelay;
	int bullets_left = tweak::tank::BulletMax;

	int health = tweak::tank::StartingShield;
	int energy = tweak::tank::StartingFuel;
	int lives_left = tweak::tank::MaxLives;

	std::shared_ptr<Controller> controller;

	Level* level;
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
	[[nodiscard]] int GetLives() const { return this->lives_left; }
	void Invalidate() { this->is_valid = false; }
    void AlterEnergy(int diff);
	void AlterHealth(int diff);

    void DoMove(class TankList* tl);
	CollisionType GetCollision(int dir, Position pos, TankList* tl);
	void TryBaseHeal();

	void Clear(DrawBuffer* drawBuff) const;
	void Draw(DrawBuffer* drawBuff) const;

	void ReturnBullet();
	void TriggerExplosion() const;
	void ApplyControllerOutput(ControllerOutput controls);
};





