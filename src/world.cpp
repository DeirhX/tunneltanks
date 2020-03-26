#include "world.h"
#include "base.h"
#include "game.h"
#include "random.h"

void World::Advance(class LevelDrawBuffer * draw_buffer)
{
    ++this->advance_count;
    RegrowPass();

    /* Clear everything: */
    this->tank_list->for_each([=](Tank * t) { t->Clear(draw_buffer); });
    this->projectile_list->Erase(draw_buffer, this->level.get());

    /* Move everything: */
    this->projectile_list->Advance(this->level.get(), this->GetTankList());
    this->tank_list->for_each([=](Tank * t) { t->Advance(this); });

    /* Draw everything: */
    this->projectile_list->Draw(draw_buffer);
    this->tank_list->for_each([=](Tank * t) { t->Draw(draw_buffer); });
}

void World::GameIsOver() { this->game->GameOver(); }

void World::RegrowPass()
{
    Stopwatch<> elapsed;
    int holes_decayed = 0;
    int dirt_grown = 0;
    this->level->ForEachVoxelParallel(
        [this, &holes_decayed, &dirt_grown](Position pos, LevelPixel & vox, ThreadLocal * local) {
            if (vox == LevelPixel::Blank || Pixel::IsScorched(vox))
            {
                int neighbors =
                    this->level->CountNeighbors(pos, [](auto voxel) { return Pixel::IsDirt(voxel) ? 1 : 0; });
                int modifier = (vox == LevelPixel::Blank) ? 4 : 1;
                if (neighbors > 2 && local->random.Int(0, 10000) < tweak::DirtRegrowSpeed * neighbors * modifier)
                {

                    vox = LevelPixel::DirtGrow;
                    this->level->CommitPixel(pos);
                    ++holes_decayed;
                }
            }
            else if (vox == LevelPixel::DirtGrow)
            {
                if (Random.Int(0, 1000) < tweak::DirtRecoverSpeed)
                {
                    vox = local->random.Bool(500) ? LevelPixel::DirtHigh : LevelPixel::DirtLow;
                    this->level->CommitPixel(pos);
                    ++dirt_grown;
                }
            }
        },
        WorkerCount{PhysicalCores{}});
    this->regrow_elapsed += elapsed.GetElapsed();
    this->regrow_average = this->regrow_elapsed / this->advance_count;
    if (this->advance_count % 100 == 1)
        DebugTrace<4>("RegrowPass takes on average %lld.%03lld ms\r\n", this->regrow_average.count() / 1000,
                      this->regrow_average.count() % 1000);
}
