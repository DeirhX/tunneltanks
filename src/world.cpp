#include "pch.h"
#include "world.h"
#include "game.h"
#include "random.h"
#include "entity.h"

namespace crust
{
World::World(Size terrain_size)
    : terrain(terrain_size), link_map(&this->terrain), projectile_list(), harvester_list(),
      tank_list(&this->terrain, &this->projectile_list), sprite_list(),
      collision_solver(&this->terrain, &this->tank_list, &this->harvester_list), sectors(SizeF(terrain_size))
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

    entity_system.Clear();
}

void World::BeginGame(Game * with_game)
{
    this->game = with_game;
    this->tank_bases.BeginGame();
    this->terrain.BeginGame();

    entity_system.Begin();
}

void World::Advance()
{
    Stopwatch<> frame_watch;

    ++this->advance_count;
    this->time_elapsed += tweak::world::AdvanceStep;

    { Stopwatch<> w; RegrowPass(); profile.regrow += w.GetElapsed(); }

    { Stopwatch<> w; this->projectile_list.Advance(&this->terrain, &this->GetTankList()); profile.projectiles += w.GetElapsed(); }
    { Stopwatch<> w; this->tank_list.for_each([=](Tank * t) { t->Advance(*this); }); profile.tanks += w.GetElapsed(); }
    { Stopwatch<> w; this->harvester_list.Advance(&this->terrain, &this->GetTankList()); profile.harvesters += w.GetElapsed(); }
    { Stopwatch<> w; this->sprite_list.Advance(&this->terrain); profile.sprites += w.GetElapsed(); }
    { Stopwatch<> w; for (TankBase & base : this->tank_bases.GetSpawns()) base.Advance(); profile.bases += w.GetElapsed(); }
    { Stopwatch<> w; this->link_map.Advance(); profile.links += w.GetElapsed(); }
    { Stopwatch<> w; this->terrain.Advance(); profile.terrain_advance += w.GetElapsed(); }
    { Stopwatch<> w; entity_system.Advance(); profile.ecs += w.GetElapsed(); }

    profile.total += frame_watch.GetElapsed();
    ++profile.frame_count;

    if (profile.frame_count >= ProfileReportInterval)
        ReportProfile();
}

void World::ReportProfile()
{
    auto avg = [&](std::chrono::microseconds acc) -> long long { return acc.count() / profile.frame_count; };
    auto ms  = [](long long us) -> long long { return us / 1000; };
    auto frac = [](long long us) -> long long { return (us % 1000); };

    long long t = avg(profile.total);
    DebugTrace<3>("[Profile] regrow=%lld.%03lld proj=%lld.%03lld tanks=%lld.%03lld harv=%lld.%03lld "
                  "spr=%lld.%03lld bases=%lld.%03lld links=%lld.%03lld terr=%lld.%03lld ecs=%lld.%03lld "
                  "| total=%lld.%03lld ms (avg over %d frames)\n",
                  ms(avg(profile.regrow)), frac(avg(profile.regrow)),
                  ms(avg(profile.projectiles)), frac(avg(profile.projectiles)),
                  ms(avg(profile.tanks)), frac(avg(profile.tanks)),
                  ms(avg(profile.harvesters)), frac(avg(profile.harvesters)),
                  ms(avg(profile.sprites)), frac(avg(profile.sprites)),
                  ms(avg(profile.bases)), frac(avg(profile.bases)),
                  ms(avg(profile.links)), frac(avg(profile.links)),
                  ms(avg(profile.terrain_advance)), frac(avg(profile.terrain_advance)),
                  ms(avg(profile.ecs)), frac(avg(profile.ecs)),
                  ms(t), frac(t),
                  profile.frame_count);
    profile.Reset();
}

void World::Draw(WorldRenderSurface & objects_surface)
{
    /* Draw everything: */
    this->projectile_list.Draw(objects_surface);
    this->tank_list.for_each([&](Tank * t) { t->Draw(objects_surface); });
    for (const TankBase & base : this->tank_bases.GetSpawns())
        base.Draw(objects_surface);
    this->harvester_list.Draw(objects_surface);
    this->sprite_list.Draw(objects_surface);
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
    this->terrain.ForEachVoxelParallel(
        [this, &holes_decayed, &dirt_grown](TerrainPixel pix, SafePixelAccessor pixel, ThreadLocal * local)
        {
            if (pix == TerrainPixel::Blank || Pixel::IsScorched(pix) ||
                this->tank_bases.CheckBaseCollision(pixel.GetPosition()))
            {
                int neighbors = //this->level->DirtPixelsAdjacent(pixel.GetPosition());
                    this->terrain.CountNeighborValues(pixel.GetPosition(),
                                                      [](auto voxel) { return Pixel::IsDirt(voxel) ? 1 : 0; });
                int modifier = (pix == TerrainPixel::Blank) ? 4 : 1;
                if (neighbors > 2 && local->random.Int(0, 1000) < tweak::world::DirtRegrowSpeed * neighbors * modifier)
                {

                    pixel.Set(TerrainPixel::DirtGrow);
                    this->terrain.CommitPixel(pixel.GetPosition());
                    ++holes_decayed;
                }
            }
            else if (pix == TerrainPixel::DirtGrow)
            {
                if (Random.Int(0, 1000) < tweak::world::DirtRecoverSpeed)
                {
                    pixel.Set(local->random.Bool(500) ? TerrainPixel::DirtHigh : TerrainPixel::DirtLow);
                    this->terrain.CommitPixel(pixel.GetPosition());
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

} // namespace MyNamespace