#pragma once
#include <types.h>


typedef struct GameDataConfig {
	char* gen;
	Size size;
	bool is_fullscreen;
	int player_count;
	int rand_seed;
} GameDataConfig;

typedef struct GameDataActive {
	class Level* lvl;
	struct TankList* tl;
	class DrawBuffer* b;
	class ProjectileList* pl;
	struct Screen* s;
} GameDataActive;

struct GameData {
	int is_active;
	int is_debug;
	union {
		GameDataConfig config;
		GameDataActive active;
	} data;
};

/* Create a default game structure: */
GameData *game_new             () ;

/* Configure a game structure: */
void      game_set_level_gen   (GameData *gd, char *gen) ;
void      game_set_level_size  (GameData *gd, Size size) ;
void      game_set_debug       (GameData *gd, bool is_debugging) ;
void      game_set_fullscreen  (GameData *gd, bool is_fullscreen) ;
void      game_set_player_count(GameData *gd, int num) ;

/* Ready a game structure for actual use: */
void      game_finalize        (GameData *gd) ;

/* Using a finalized game structure: */
int       game_step            (void *gd) ;

/* Done with a game structure: */
void      game_free            (GameData *gd) ;



