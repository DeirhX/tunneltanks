#include <tank.h>
#include <level.h>
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
	
	this->level = lvl;
    this->pl = pl;
	 
	this->bullet_timer = tweak::tank::BulletDelay;
	this->bullets_left = tweak::tank::BulletMax;
	this->is_shooting = false;
	this->health = TANK_STARTING_SHIELD;
	this->energy = TANK_STARTING_FUEL;
}

/* We don't use the Tank structure in this function, since we are checking the
 * tank's hypothetical position... ie: IF we were here, would we collide? */
CollisionType Tank::GetCollision(int dir, Position position, TankList *tl) {
	Offset off;
	CollisionType out = CollisionType::None;
	
	/* Level Collisions: */
	for(off.y=-3; off.y<=3; off.y++)
		for(off.x=-3; off.x<=3; off.x++) {
			char c = TANK_SPRITE[dir][3+ off.y][3+ off.x];
			if(!c) continue;
			
			LevelVoxel v = this->level->GetVoxel(position + off);
			
			if(Voxels::IsDirt(v)) out = CollisionType::Dirt;
			
			if(Voxels::IsBlockingCollision(v)) return CollisionType::Blocked;
		}
	
	/* Tank collisions: */
	if(tl->CheckForCollision(*this, position, dir)) return CollisionType::Blocked;
	return out;
}

void Tank::DoMove(TankList *tl) {
	
	/* Don't let this tank do anything if it is dead: */
	if(!this->health) return;
	
	/* Calculate all of our motion: */
	if(this->controller) {
		Vector         base = this->level->GetSpawn(this->color);
		PublicTankInfo i = {
			.health = this->health,
			.energy = this->energy,
			.x      = static_cast<int>(this->pos.x - base.x),
			.y      = static_cast<int>(this->pos.y - base.y),
			.level_view  = LevelView(this, this->level)};
		
		this->ApplyControllerOutput(this->controller->ApplyControls(&i));
	}
	
	/* Calculate the direction: */
	if(this->speed.x != 0 || this->speed.y != 0) {
		int newdir = static_cast<int> ((this->speed.x+1) + (this->speed.y+1) * 3);
		
		CollisionType collision = this->GetCollision(newdir, this->pos + 1 * this->speed, tl);
		/* Now, is there room to move forward in that direction? */
		if( collision != CollisionType::Blocked ) {

			this->level->DigHole(this->pos + (1 * this->speed));
			/* If so, then we can move: */
			if (collision != CollisionType::Dirt || this->is_shooting)
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
			this->bullet_timer = tweak::tank::BulletDelay;
		}
	} else this->bullet_timer--;
}

/* Check to see if we're in any bases, and heal based on that: */
void Tank::TryBaseHeal()
{
	BaseCollision c = this->level->CheckBaseCollision({this->pos.x, this->pos.y}, this->color);
	if(c == BaseCollision::Yours) {
		this->AlterEnergy(TANK_HOME_CHARGE);
		this->AlterHealth(TANK_HOME_HEAL);
	} else if(c == BaseCollision::Enemy)
		this->AlterEnergy(TANK_ENEMY_CHARGE);
}

void Tank::Draw(DrawBuffer *drawBuff) const
{
	if(!this->health) return;
	
	for(int y=0; y<7; y++)
		for(int x=0; x<7; x++) {
			char val = TANK_SPRITE[this->direction][y][x];
			if(val)
				drawBuff->SetPixel(Position{ this->pos.x + x - 3, this->pos.y + y - 3 }, Palette.GetTank(this->color)[val - 1]);
		}
}

void Tank::Clear(DrawBuffer *drawBuff) const
{
	if(!this->health) return;
	
	for(int y=0; y<7; y++)
		for(int x=0; x<7; x++)
			if(TANK_SPRITE[this->direction][y][x])
				level->CommitPixel(Position{ this->pos.x + x - 3, this->pos.y + y - 3 });
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
		this->pos, this->level,
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
