#include "world.h"
#include "base.h"
#include "game.h"
#include "random.h"

World::World(Game * game, std::unique_ptr<Level> && level)
    : game(game), level(std::move(level)),
      projectile_list(),
      harvester_list(),
      tank_list(this->level.get(), &this->projectile_list),
      link_map(this->level.get()),
      collision_solver(this->level.get(), &this->tank_list, &this->harvester_list)
{
    this->level->OnConnectWorld(this);
}

void World::Advance()
{
    ++this->advance_count;
    RegrowPass();

    /* Move everything: */
    this->projectile_list.Advance(this->level.get(), this->GetTankList());
    this->tank_list.for_each([=](Tank * t) { t->Advance(this); });
    this->harvester_list.Advance(this->level.get(), this->GetTankList());
    /* TODO: get out of level? */
    for (TankBase & base : this->level->GetSpawns())
        base.Advance();
    this->link_map.Advance();
}


void World::Draw(WorldRenderSurface * objects_surface)
{
    /* Draw everything: */
    this->projectile_list.Draw(objects_surface);
    this->harvester_list.Draw(objects_surface);
    this->tank_list.for_each([=](Tank * t) { t->Draw(objects_surface); });
    for (const TankBase & base : this->level->GetSpawns())
        base.Draw(objects_surface);
    this->link_map.Draw(objects_surface);
}

void World::GameIsOver() { this->game->GameOver(); }


void World::RegrowPass()
{
    if (!this->regrow_timer.AdvanceAndCheckElapsed())
        return;

    Stopwatch<> elapsed;
    int holes_decayed = 0;
    int dirt_grown = 0;
    this->level->ForEachVoxelParallel(
        [this, &holes_decayed, &dirt_grown](LevelPixel pix, SafePixelAccessor pixel, ThreadLocal * local) {
            if (pix == LevelPixel::Blank || Pixel::IsScorched(pix))
            {
                int neighbors = //this->level->DirtPixelsAdjacent(pixel.GetPosition());
                    this->level->CountNeighborValues(pixel.GetPosition(), [](auto voxel) { return Pixel::IsDirt(voxel) ? 1 : 0; });
                int modifier = (pix == LevelPixel::Blank) ? 4 : 1;
                if (neighbors > 2 && local->random.Int(0, 1000) < tweak::world::DirtRegrowSpeed * neighbors * modifier)
                {

                    pixel.Set(LevelPixel::DirtGrow);
                    this->level->CommitPixel(pixel.GetPosition());
                    ++holes_decayed;
                }
            }
            else if (pix == LevelPixel::DirtGrow)
            {
                if (Random.Int(0, 1000) < tweak::world::DirtRecoverSpeed)
                {
                    pixel.Set(local->random.Bool(500) ? LevelPixel::DirtHigh : LevelPixel::DirtLow);
                    this->level->CommitPixel(pixel.GetPosition());
                    ++dirt_grown;
                }
            }
        },
        WorkerCount{PhysicalCores{}});

    this->regrow_elapsed += elapsed.GetElapsed();
    if (this->advance_count % 100 == 1)
    {
        this->regrow_average = this->regrow_elapsed / this->advance_count;
        DebugTrace<4>("RegrowPass takes on average %lld.%03lld ms\r\n", this->regrow_average.count() / 1000,
                      this->regrow_average.count() % 1000);
    }
}
