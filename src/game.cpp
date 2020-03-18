#include "base.h"
#include <cstdlib>

#include <game.h>
#include <bitmaps.h>
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
#include <world.h>
#include <random.h>
#include "exceptions.h"
#include "colors.h"


#define ERR_OUT(msg) gamelib_error( "PROGRAMMING ERROR: " msg )

/*----------------------------------------------------------------------------*
 * This bit is used to initialize various GUIs:                               *
 *----------------------------------------------------------------------------*/

void GameMode::AssumeAIControl(TankList *tl, Level *lvl, TankColor starting_id) {
	
	for(TankColor i=starting_id; i< tweak::MaxPlayers; i++) {
		Tank *t = tl->AddTank(i, lvl->GetSpawn(i));
		t->SetController(std::make_shared<TwitchController>());
	}
}


std::unique_ptr<SinglePlayerMode> SinglePlayerMode::Setup(Screen* screen, World* world)
{

	/* Ready the tank! */
	Tank* t = world->GetTankList()->AddTank(0, world->GetLevel()->GetSpawn(0));
	gamelib_tank_attach(t, 0, 1);

	Screens::SinglePlayerScreenSetup(screen, world, t);

	/* Fill up the rest of the slots with Twitches: */
	GameMode::AssumeAIControl(world->GetTankList(), world->GetLevel(), 1);

	return std::unique_ptr<SinglePlayerMode>{ new SinglePlayerMode(screen, world) }; // Can't make unique, private constructor
}

void SinglePlayerMode::TearDown()
{
	this->screen->ClearGuiElements();
	this->screen = nullptr;
	this->world = nullptr;
}

std::unique_ptr<LocalTwoPlayerMode> LocalTwoPlayerMode::Setup(Screen* screen, World* world)
{
	/* Ready the tanks! */
	Tank* player_one = world->GetTankList()->AddTank(0, world->GetLevel()->GetSpawn(0));
	gamelib_tank_attach(player_one, 0, 2);

	/* Load up two controllable tanks: */
	Tank* player_two = world->GetTankList()->AddTank(1, world->GetLevel()->GetSpawn(1));
	/*controller_twitch_attach(t);  << Attach a twitch to a camera tank, so we can see if they're getting smarter... */
	gamelib_tank_attach(player_two, 1, 2);

	Screens::TwoPlayerScreenSetup(screen, world, player_one, player_two);
	/* Fill up the rest of the slots with Twitches: */
	GameMode::AssumeAIControl(world->GetTankList(), world->GetLevel(), 2);

	return std::unique_ptr<LocalTwoPlayerMode>{ new LocalTwoPlayerMode(screen, world) }; // Can't make unique, private constructor
}

void LocalTwoPlayerMode::TearDown()
{
	this->screen->ClearGuiElements();
	this->screen = nullptr;
	this->world = nullptr;
}


/* Create a default game structure: */
Game::Game(GameConfig config) {
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

	/* Create projectile list, tank list and materialize the level voxels */
	auto projectile_list = std::make_unique<ProjectileList>();
	auto tank_list = std::make_unique<TankList>(level.get(), projectile_list.get());
	level->GenerateDirtAndRocks();
	level->CreateBases();
	
	/* Debug the starting data, if we're debugging: */
	if(this->is_debug)
		level->DumpBitmap("debug_start.bmp");
	
	/* Push the level to the draw buffer */
	this->draw_buffer->SetDefaultColor(Palette.Get(Colors::Rock));
	level->CommitAll();
	this->screen->SetLevelDrawMode(this->draw_buffer.get());

	/* Create the world */
	world = std::make_unique<World>(this, std::move(tank_list), std::move(projectile_list), std::move(level));
	
	/* Set up the players/GUI inside the world */
	if (this->config.player_count > gamelib_get_max_players())
		throw GameException("Tried to use more players than the platform can support.");
	if     (this->config.player_count == 1) this->mode = SinglePlayerMode::Setup(this->screen.get(), this->world.get());
	else if(this->config.player_count == 2) this->mode = LocalTwoPlayerMode::Setup(this->screen.get(), this->world.get());
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
	
	world->Advance(this->draw_buffer.get());
	this->screen->DrawCurrentMode();
	
	return true;
}

void Game::GameOver()
{
	assert(!"Implement game over!");
}

/* Done with a game structure: */
Game::~Game() {
	if(this->is_active) 
	{
		/* Debug if we need to: */
		if(this->is_debug)
		  world->GetLevel()->DumpBitmap("debug_end.bmp");
		this->mode->TearDown();
	}
}
