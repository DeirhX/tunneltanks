#include <cstdio>
#include <cstdlib>

#include <game.h>
#include <controller.h>
#include <guisprites.h>
#include <level.h>
#include <levelgen.h>
#include <tanklist.h>
#include <drawbuffer.h>
#include <projectile.h>
#include <screen.h>
#include <memalloc.h>
#include <tweak.h>
#include <gamelib.h>
#include <chrono>
#include <aitwitch.h>
#include <random.h>
#include "exceptions.h"


#define ERR_OUT(msg) gamelib_error( "PROGRAMMING ERROR: " msg )

#define ASSERT_CONFIG() do { \
	if(gd->is_active) { \
		ERR_OUT("Attempted to configure a game while it is running."); \
		exit(1); \
	} \
} while(0)

#define ASSERT_ACTIVE() do { \
	if(!gd->is_active) { \
		ERR_OUT("Attempted to start a game before finalizing."); \
		exit(1); \
	} \
} while(0)


/*----------------------------------------------------------------------------*
 * This bit is used to initialize various GUIs:                               *
 *----------------------------------------------------------------------------*/

static void twitch_fill(TankList *tl, Level *lvl, TankColor starting_id) {
	
	for(TankColor i=starting_id; i<MAX_TANKS; i++) {
		Tank *t = tl->AddTank(i, lvl->GetSpawn(i));
		t->SetController(std::make_shared<TwitchController>());
	}
}


/* TODO: De-uglify this crap: */
static void init_single_player(Screen *s, TankList *tl, Level *lvl) {
	Tank *t;
	Rect gui;
	int gui_shift;
	
	/* Account for the GUI Controller: */
	gui = gamelib_gui_get_size();
	gui_shift = gui.size.x + !!gui.size.x * 15; /* << Shift out of way of thumb... */
	
	gamelib_debug("XYWH: %u %u %u %u", gui.pos.x, gui.pos.y, gui.size.x, gui.size.y);
	
	/* Ready the tank! */
	t = tl->AddTank(0, lvl->GetSpawn(0));
	gamelib_tank_attach(t, 0, 1);
	
	s->AddWindow(Rect{ Position{ 2, 2 }, Size {GAME_WIDTH - 4, GAME_HEIGHT - 6 - STATUS_HEIGHT} }, t);
	s->AddStatus(Rect(9 + gui_shift, GAME_HEIGHT - 2 - STATUS_HEIGHT, GAME_WIDTH-12 - gui_shift, STATUS_HEIGHT), t, 1);
	if(gui_shift)
		s->AddController( Rect(3, GAME_HEIGHT - 5 - static_cast<int>(gui.size.y), gui.size.x, gui.size.y));
	
	/* Add the GUI bitmaps: */
	s->AddBitmap(Rect(3 + gui_shift, GAME_HEIGHT - 2 - STATUS_HEIGHT    , 4, 5), GUI_ENERGY, &color_status_energy);
	s->AddBitmap(Rect(3 + gui_shift, GAME_HEIGHT - 2 - STATUS_HEIGHT + 6, 4, 5), GUI_HEALTH, &color_status_health);
	
	/* Fill up the rest of the slots with Twitches: */
	twitch_fill(tl, lvl, 1);
}

static void init_double_player(Screen *s, TankList *tl, Level *lvl) {
	Tank *t;
	
	/* Ready the tanks! */
	t = tl->AddTank(0, lvl->GetSpawn(0));
	gamelib_tank_attach(t, 0, 2);
	s->AddWindow(Rect(2, 2, GAME_WIDTH/2-3, GAME_HEIGHT-6-STATUS_HEIGHT), t);
	s->AddStatus(Rect(3, GAME_HEIGHT - 2 - STATUS_HEIGHT, GAME_WIDTH/2-5-2, STATUS_HEIGHT), t, 0);
	
	/* Load up two controllable tanks: */
	t = tl->AddTank(1, lvl->GetSpawn(1));
	
	/*controller_twitch_attach(t);  << Attach a twitch to a camera tank, so we can see if they're getting smarter... */
	gamelib_tank_attach(t, 1, 2);
	s->AddWindow(Rect(GAME_WIDTH/2+1, 2, GAME_WIDTH/2-3, GAME_HEIGHT-6-STATUS_HEIGHT), t);
	s->AddStatus(Rect(GAME_WIDTH/2+2+2, GAME_HEIGHT - 2 - STATUS_HEIGHT, GAME_WIDTH/2-5-3, STATUS_HEIGHT), t, 1);

	/* Add the GUI bitmaps: */
	s->AddBitmap(Rect(GAME_WIDTH/2-2, GAME_HEIGHT - 2 - STATUS_HEIGHT    , 4, 5), GUI_ENERGY, &color_status_energy);
	s->AddBitmap(Rect(GAME_WIDTH/2-2, GAME_HEIGHT - 2 - STATUS_HEIGHT + 6, 4, 5), GUI_HEALTH, &color_status_health);
	
	/* Fill up the rest of the slots with Twitches: */
	twitch_fill(tl, lvl, 2);
}

/* Create a default game structure: */
Game::Game(GameDataConfig config) {
	/* Copy in all the default values: */
	this->config = config;
	this->is_active = 0;
	this->is_debug = config.is_debug;
	
	/* The hell was I thinking?
	out->data.config.w = GAME_WIDTH;
	out->data.config.h = GAME_HEIGHT;
	*/
	if(gamelib_get_can_window())          this->config.is_fullscreen = false;
	else if(gamelib_get_can_fullscreen()) this->config.is_fullscreen = true;
	else {
		/* The hell!? */
		throw GameException("gamelib can't run fullscreen or in a window.");
	}
	
	/* Initialize most of the structures: */
	this->screen   = std::make_unique<Screen>(this->config.is_fullscreen);
	this->projectiles  = std::make_unique<ProjectileList>();
	this->draw_buffer   = std::make_unique<DrawBuffer>(this->config.size);
	
	/* Generate our random level: */
	int TestIterations = 30;
#ifdef _DEBUG
	TestIterations = 3;
#endif
	std::chrono::milliseconds time_taken = {};
	for (int i = TestIterations; i-- > 0; ) {
		this->level = std::make_unique<Level>(this->config.size, this->draw_buffer.get());
		time_taken += generate_level(this->level.get(), this->config.level_generator);
	}
	auto average_time = time_taken / TestIterations;
	gamelib_print("***\r\nAverage level time: %lld.%03lld sec\n", average_time.count() / 1000, average_time.count() % 1000);
	
	this->tank_list = std::make_unique<TankList>(this->level.get(), this->projectiles.get());
	this->level->GenerateDirtAndRocks();
	this->level->CreateBases();
	
	/* Debug the starting data, if we're debugging: */
	if(this->is_debug)
		this->level->DumpBitmap("debug_start.bmp");
	
	/* Start drawing! */
	this->draw_buffer->SetDefaultColor(color_rock);
	this->level->CommitAll();
	this->screen->SetLevelDrawMode(this->draw_buffer.get());
	
	/* Set up the players/GUI: */
	if (this->config.player_count > gamelib_get_max_players())
		throw GameException("Tried to use more players than the platform can support.");
	if     (this->config.player_count == 1) init_single_player(this->screen.get(), this->tank_list.get(), this->level.get());
	else if(this->config.player_count == 2) init_double_player(this->screen.get(), this->tank_list.get(), this->level.get());
	else {
		ERR_OUT("Don't know how to draw more than 2 players at once...");
		exit(1);
	}
	
	/* Copy all of our variables into the GameData struct: */
	this->is_active = 1;
}

/* Step the game simulation by handling events, and drawing: */
bool Game::AdvanceStep() {
	assert(this->is_active);
	
	GameEvent temp;
	
	/* Handle all queued events: */
	while( (temp=gamelib_event_get_type()) != GameEvent::None ) {
		
		/* Trying to resize the window? */
		if(temp == GameEvent::Resize) {
			Rect r = gamelib_event_resize_get_size();
			this->screen->Resize(r.size);
		
		/* Trying to toggle fullscreen? */
		} else if(temp == GameEvent::ToggleFullscreen) {
			this->screen->SetFullscreen(this->screen->GetFullscreen());
		
		/* Trying to exit? */
		} else if(temp == GameEvent::Exit) {
			return false;
		}
		
		/* Done with this event: */
		gamelib_event_done();
	}
	
	/* Grow */
	DecayPass();

	/* Clear everything: */
	this->tank_list->for_each([=](Tank* t) {t->Clear(this->draw_buffer.get()); });
	this->projectiles->Erase(this->draw_buffer.get());

	/* Charge a small bit of energy for life: */
	this->tank_list->for_each([=](Tank* t) {t->AlterEnergy(TANK_IDLE_COST); });

	/* See if we need to be healed: */
	this->tank_list->for_each([=](Tank* t) {t->TryBaseHeal(); });
	
	/* Move everything: */
	this->projectiles->Advance(this->level.get(), this->tank_list.get());
	this->tank_list->for_each([=](Tank* t) {t->DoMove(this->tank_list.get()); });
	
	/* Draw everything: */
	this->projectiles->Draw(this->draw_buffer.get());
	this->tank_list->for_each([=](Tank* t) {t->Draw(this->draw_buffer.get()); });
	this->screen->DrawCurrentMode();
	return true;
}

void Game::DecayPass()
{
	int holes_decayed = 0;
	this->level->ForEachVoxel([this, &holes_decayed](Position pos, LevelVoxel& vox)
		{
			if (vox == LevelVoxel::Blank)
			{
				int neighbors = this->level->CountNeighbors(pos, LevelVoxel::Rock);
				if (neighbors > 2 && Random.Bool(1000 * neighbors)) {
					vox = Random.Bool(500) ? LevelVoxel::DirtHigh : LevelVoxel::DirtLow;
					this->level->CommitPixel(pos);
					++holes_decayed;
				}
			}
		}
	);
}

/* Done with a game structure: */
Game::~Game() {
	if(this->is_active) {
		/* Debug if we need to: */
		if(this->is_debug)
		  this->level->DumpBitmap("debug_end.bmp");
	}
}

