#include "world.h"
#include "game.h"
#include "random.h"

World::World(Game * game, std::unique_ptr<Terrain> && level)
    : game(game), terrain(std::move(level)),
      link_map(this->terrain.get()),
      projectile_list(),
      harvester_list(),
      tank_list(this->terrain.get(), &this->projectile_list),
      sprite_list(),
      collision_solver(this->terrain.get(), &this->tank_list, &this->harvester_list)
{
    //this->level->OnConnectWorld(this);
}

void World::Clear()
{
    this->projectile_list.RemoveAll();
    this->tank_list.RemoveAll();
    this->harvester_list.RemoveAll();
    this->sprite_list.RemoveAll();
    this->link_map.RemoveAll();
}

void World::BeginGame() { this->terrain->BeginGame(); }

void World::Advance()
{
    ++this->advance_count;
    this->time_elapsed += tweak::world::AdvanceStep;
    RegrowPass();

    /* Move everything: */
    this->projectile_list.Advance(this->terrain.get(), this->GetTankList());
    this->tank_list.for_each([=](Tank * t) { t->Advance(*this); });
    this->harvester_list.Advance(this->terrain.get(), this->GetTankList());
    this->sprite_list.Advance(this->terrain.get());
    /* TODO: get out of level? */
    for (TankBase & base : this->terrain->GetSpawns())
        base.Advance();
    this->link_map.Advance();
}


void World::Draw(WorldRenderSurface * objects_surface)
{
    /* Draw everything: */
    this->projectile_list.Draw(objects_surface);
    this->tank_list.for_each([=](Tank * t) { t->Draw(*objects_surface); });
    for (const TankBase & base : this->terrain->GetSpawns())
        base.Draw(objects_surface);
    this->harvester_list.Draw(objects_surface);
    this->sprite_list.Draw(*objects_surface);
    this->link_map.Draw(objects_surface);
}

void World::SetGameOver() { this->game->GameOver(); }


void World::RegrowPass()
{
    if (!this->regrow_timer.AdvanceAndCheckElapsed())
        return;

    Stopwatch<> elapsed;
    int holes_decayed = 0;
    int dirt_grown = 0;
    this->terrain->ForEachVoxelParallel(
        [this, &holes_decayed, &dirt_grown](TerrainPixel pix, SafePixelAccessor pixel, ThreadLocal * local) {
            if (pix == TerrainPixel::Blank || Pixel::IsScorched(pix) || this->GetTerrain()->CheckBaseCollision(pixel.GetPosition()))
            {
                int neighbors = //this->level->DirtPixelsAdjacent(pixel.GetPosition());
                    this->terrain->CountNeighborValues(pixel.GetPosition(), [](auto voxel) { return Pixel::IsDirt(voxel) ? 1 : 0; });
                int modifier = (pix == TerrainPixel::Blank) ? 4 : 1;
                if (neighbors > 2 && local->random.Int(0, 1000) < tweak::world::DirtRegrowSpeed * neighbors * modifier)
                {

                    pixel.Set(TerrainPixel::DirtGrow);
                    this->terrain->CommitPixel(pixel.GetPosition());
                    ++holes_decayed;
                }
            }
            else if (pix == TerrainPixel::DirtGrow)
            {
                if (Random.Int(0, 1000) < tweak::world::DirtRecoverSpeed)
                {
                    pixel.Set(local->random.Bool(500) ? TerrainPixel::DirtHigh : TerrainPixel::DirtLow);
                    this->terrain->CommitPixel(pixel.GetPosition());
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
