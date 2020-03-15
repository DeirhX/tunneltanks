#include "base.h"
#include "world.h"
#include "random.h"


void World::Advance(class DrawBuffer* draw_buffer)
{
	++this->advance_count;
	RegrowPass();
	
	/* Clear everything: */
	this->tank_list->for_each([=](Tank* t) {t->Clear(draw_buffer); });
	this->projectiles->Erase(draw_buffer, this->level.get());

	/* Charge a small bit of energy for life: */
	this->tank_list->for_each([=](Tank* t) {t->AlterEnergy(tweak::tank::IdleCost); });

	/* See if we need to be healed: */
	this->tank_list->for_each([=](Tank* t) {t->TryBaseHeal(); });

	/* Move everything: */
	this->projectiles->Advance(this->level.get(), this->tank_list.get());
	this->tank_list->for_each([=](Tank* t) {t->DoMove(this->tank_list.get()); });

	/* Draw everything: */
	this->projectiles->Draw(draw_buffer);
	this->tank_list->for_each([=](Tank* t) {t->Draw(draw_buffer); });

}

void World::RegrowPass()
{
	Stopwatch<> elapsed;
	int holes_decayed = 0;
	int dirt_grown = 0;
	this->level->ForEachVoxelParallel([this, &holes_decayed, &dirt_grown](Position pos, LevelVoxel& vox, ThreadLocal* local)
	{
		if (vox == LevelVoxel::Blank || Voxels::IsScorched(vox))
		{
			int neighbors = this->level->CountNeighbors(pos, [](auto voxel) { return Voxels::IsDirt(voxel) ? 1 : 0; });
			int modifier = (vox == LevelVoxel::Blank) ? 3 : 1;
			if (neighbors > 2 && local->random.Int(0, 500) < tweak::DirtRegrowSpeed * neighbors * modifier) {

				vox = LevelVoxel::DirtGrow;
				this->level->CommitPixel(pos);
				++holes_decayed;
			}
		}
		else if (vox == LevelVoxel::DirtGrow)
		{
			if (Random.Int(0, 1000) < tweak::DirtRecoverSpeed) {
				vox = local->random.Bool(500) ? LevelVoxel::DirtHigh : LevelVoxel::DirtLow;
				this->level->CommitPixel(pos);
				++dirt_grown;
			}
		}
	}, WorkerCount{PhysicalCores{}}
	);
	this->regrow_elapsed += elapsed.GetElapsed();
	this->regrow_average = this->regrow_elapsed / this->advance_count;
	if (this->advance_count % 100 == 1)
		DebugTrace<4>("RegrowPass takes on average %lld.%03lld ms\r\n", this->regrow_average.count() / 1000, this->regrow_average.count() % 1000);
}
