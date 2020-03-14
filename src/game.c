#include <cstdlib>

#include <game.h>
#include <guisprites.h>
#include <level.h>
#include <levelgen.h>
#include <tanklist.h>
#include <drawbuffer.h>
#include <projectile.h>
#include <screen.h>
#include <tweak.h>
#include <gamelib.h>
#include <chrono>
#include <aitwitch.h>
#include <random.h>
#include "exceptions.h"
#include "colors.h"


#define ERR_OUT(msg) gamelib_error( "PROGRAMMING ERROR: " msg )

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
	/* Account for the GUI Controller: */
	Rect gui = gamelib_gui_get_size();
	int gui_shift = gui.size.x + !!gui.size.x * 15; /* << Shift out of way of thumb... */
	
	gamelib_debug("XYWH: %u %u %u %u", gui.pos.x, gui.pos.y, gui.size.x, gui.size.y);
	
	/* Ready the tank! */
	Tank* t = tl->AddTank(0, lvl->GetSpawn(0));
	gamelib_tank_attach(t, 0, 1);
	
	s->AddWindow(Rect{ Position{ 2, 2 }, Size {GAME_WIDTH - 4, GAME_HEIGHT - 6 - tweak::screen::status_height} }, t);
	s->AddStatus(Rect(9 + gui_shift, GAME_HEIGHT - 2 - tweak::screen::status_height, GAME_WIDTH-12 - gui_shift, tweak::screen::status_height), t, 1);
	if(gui_shift)
		s->AddController( Rect(3, GAME_HEIGHT - 5 - static_cast<int>(gui.size.y), gui.size.x, gui.size.y));
	
	/* Add the GUI bitmaps: */
	s->AddBitmap(Rect(3 + gui_shift, GAME_HEIGHT - 2 - tweak::screen::status_height    , 4, 5), GUI_ENERGY, Palette.Get(Colors::StatusEnergy));
	s->AddBitmap(Rect(3 + gui_shift, GAME_HEIGHT - 2 - tweak::screen::status_height + 6, 4, 5), GUI_HEALTH, Palette.Get(Colors::StatusHealth));
	
	/* Fill up the rest of the slots with Twitches: */
	twitch_fill(tl, lvl, 1);
}

static void init_double_player(Screen *s, TankList *tl, Level *lvl) {
	/* Ready the tanks! */
	Tank* t = tl->AddTank(0, lvl->GetSpawn(0));
	gamelib_tank_attach(t, 0, 2);
	s->AddWindow(Rect(2, 2, GAME_WIDTH/2-3, GAME_HEIGHT-6-tweak::screen::status_height), t);
	s->AddStatus(Rect(3, GAME_HEIGHT - 2 - tweak::screen::status_height, GAME_WIDTH/2-5-2, tweak::screen::status_height), t, 0);
	
	/* Load up two controllable tanks: */
	t = tl->AddTank(1, lvl->GetSpawn(1));
	
	/*controller_twitch_attach(t);  << Attach a twitch to a camera tank, so we can see if they're getting smarter... */
	gamelib_tank_attach(t, 1, 2);
	s->AddWindow(Rect(GAME_WIDTH/2+1, 2, GAME_WIDTH/2-3, GAME_HEIGHT-6-tweak::screen::status_height), t);
	s->AddStatus(Rect(GAME_WIDTH/2+2+2, GAME_HEIGHT - 2 - tweak::screen::status_height, GAME_WIDTH/2-5-3, tweak::screen::status_height), t, 1);

	/* Add the GUI bitmaps: */
	s->AddBitmap(Rect(GAME_WIDTH/2-2, GAME_HEIGHT - 2 - tweak::screen::status_height    , 4, 5), GUI_ENERGY, Palette.Get(Colors::StatusEnergy));
	s->AddBitmap(Rect(GAME_WIDTH/2-2, GAME_HEIGHT - 2 - tweak::screen::status_height + 6, 4, 5), GUI_HEALTH, Palette.Get(Colors::StatusHealth));
	
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
	this->world.projectiles  = std::make_unique<ProjectileList>();
	this->draw_buffer   = std::make_unique<DrawBuffer>(this->config.size);
	
	/* Generate our random level: */
	int TestIterations = 10;
#ifdef _DEBUG
	TestIterations = 1;
#endif
	std::chrono::milliseconds time_taken = {};
	std::unique_ptr<Level> level;
	for (int i = TestIterations; i-- > 0; ) {
		level = std::make_unique<Level>(this->config.size, this->draw_buffer.get());
		time_taken += generate_level(level.get(), this->config.level_generator);
	}
	auto average_time = time_taken / TestIterations;
	gamelib_print("***\r\nAverage level time: %lld.%03lld sec\n", average_time.count() / 1000, average_time.count() % 1000);
	
	this->world.tank_list = std::make_unique<TankList>(level.get(), this->world.projectiles.get());
	level->GenerateDirtAndRocks();
	level->CreateBases();
	
	/* Debug the starting data, if we're debugging: */
	if(this->is_debug)
		level->DumpBitmap("debug_start.bmp");
	
	/* Start drawing! */
	this->draw_buffer->SetDefaultColor(Palette.Get(Colors::Rock));
	level->CommitAll();
	this->screen->SetLevelDrawMode(this->draw_buffer.get());
	
	/* Set up the players/GUI: */
	if (this->config.player_count > gamelib_get_max_players())
		throw GameException("Tried to use more players than the platform can support.");
	if     (this->config.player_count == 1) init_single_player(this->screen.get(), this->world.tank_list.get(), level.get());
	else if(this->config.player_count == 2) init_double_player(this->screen.get(), this->world.tank_list.get(), level.get());
	else {
		ERR_OUT("Don't know how to draw more than 2 players at once...");
		exit(1);
	}

	world.level = std::move(level);
	
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
	
	world.Advance(this->draw_buffer.get());
	this->screen->DrawCurrentMode();
	
	return true;
}

/* Done with a game structure: */
Game::~Game() {
	if(this->is_active) {
		/* Debug if we need to: */
		if(this->is_debug)
		  world.level->DumpBitmap("debug_end.bmp");
	}
}

