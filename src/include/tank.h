#pragma once
#include <memory>
/* For the controllers/AIs: */

#include <level.h>
#include <drawbuffer.h>
#include <level_view.h>
#include <controllersdl.h>
#include <projectile_list.h>
//#include <world.h>

struct LevelView;
class World;

/* Put inside a structure, so we are protected from casual AI cheating: */
struct PublicTankInfo
{
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
	bool is_valid = false;
	bool is_shooting = false;

	Position pos;
	Speed speed; /* Velocity... ie: is it moving now? */
	int direction; // TODO: Modernize

	TankColor color; /* Unique id and also color of the tank */

	int bullet_timer = tweak::tank::BulletDelay;
	int bullets_left = tweak::tank::BulletMax;
	int respawn_timer = 0;

	int health = tweak::tank::StartingShield;
	int energy = tweak::tank::StartingFuel;
	int lives_left = tweak::tank::MaxLives;

	std::shared_ptr<Controller> controller;

	Level* level;
	ProjectileList* pl;

public:
	void Invalidate() { this->is_valid = false; }
	
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
	[[nodiscard]] Level* GetLevel() { return this->level; };

	void Advance(World* world); /* Advance world-step */
		
    void AlterEnergy(int diff);
	void AlterHealth(int diff);

	void Spawn();
	void Die();

	void ApplyControllerOutput(ControllerOutput controls);

	CollisionType GetCollision(int dir, Position pos, TankList* tl);

	void Clear(DrawBuffer* drawBuff) const;
	void Draw(DrawBuffer* drawBuff) const;

	void ReturnBullet();
private:
	void DoMove(class TankList* tl);
	void TryBaseHeal();
};

