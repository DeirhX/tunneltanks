#include <cstdio>
#include <cstring>
#include <ctime>
#include <chrono>
#include <gamelib.h>
#include <levelgen.h>
#include <algorithm>


typedef struct LevelGenerator {
	const char         *id;
	LevelGeneratorFunc gen;
	const char         *desc;
} LevelGenerator;
#define LEVEL_GENERATOR(id, gen, desc) {(id), (gen), (desc)}


/* === All the generator headers go here: =================================== */

#include <levelgentoast.h>
#include <levelgensimple.h>
#include <levelgenmaze.h>
#include <levelgenbraid.h>

/* Add an entry for every generator: */
LevelGenerator GENERATOR_LIST[] =
{
	LEVEL_GENERATOR("toast",  levelgen::toast::toast_generator,  "Twisty, cavernous maps." ),
	
	LEVEL_GENERATOR("braid",  levelgen::braid::braid_generator,  "Maze-like maps with no dead ends."),
	LEVEL_GENERATOR("maze",   levelgen::maze::maze_generator,   "Complicated maps with a maze surrounding the bases."),
	LEVEL_GENERATOR("simple", levelgen::simple::simple_generator, "Simple rectangular maps with ragged sides."),
	
	/* This needs to be the last item in the list: */
	LEVEL_GENERATOR(NULL, NULL, NULL)
};

/* ========================================================================== */

/* Linear search is ok here, since there aren't many level generators: */
std::chrono::milliseconds generate_level(Level *lvl, const char *id) {
	LevelGeneratorFunc func = NULL;
	int i;
	clock_t t;
	
	/* If 'id' is null, go with the default: */
	if(!id) id = GENERATOR_LIST[0].id;
	
	/* Look for the id: */
	for(i=0; GENERATOR_LIST[i].id; i++) {
		if(!strcmp(id, GENERATOR_LIST[i].id)) {
			gamelib_print("Using level generator: '%s'\n", GENERATOR_LIST[i].id);
			func = GENERATOR_LIST[i].gen;
			goto generate_level;
		}
	}
	
	/* Report what level generator we found: */
	gamelib_print("Couldn't find level generator: '%s'\n", id);
	gamelib_print("Using default level generator: '%s'\n", GENERATOR_LIST[0].id);
	
	/* If we didn't find the id, then we select the default: */
	if(!func) func = GENERATOR_LIST[0].gen;
	
generate_level:

	{
		Stopwatch<std::chrono::milliseconds> s;

		/* Ok, now generate the level: */
		func(lvl);

		gamelib_print("Level generated in: ");
		auto msecs = s.GetElapsed();
		gamelib_print("%u.%03u sec\n", msecs / 1000, msecs % 1000);

		return { std::chrono::duration_cast<std::chrono::milliseconds>(msecs)};
	}
}

/* Will print a specified number of spaces to the file: */
static void put_chars(size_t i, char c) {
	while( i-- )
		gamelib_print("%c", c);
}

void print_levels(FILE *out) {
	int i;
	size_t max_id = 7;
	size_t max_desc = strlen("Description:");
	
	/* Get the longest ID/Description length: */
	for(i=0; GENERATOR_LIST[i].id; i++) {
		max_id = std::max(max_id, strlen(GENERATOR_LIST[i].id));
		max_desc = std::max(max_desc, strlen(GENERATOR_LIST[i].desc));
	}
	
	/* Print the header: */
	gamelib_print("ID:  ");
	put_chars(max_id - strlen("ID:"), ' ');
	gamelib_print("Description:\n");
	put_chars(max_id + max_desc + 2, '-');
	gamelib_print("\n");
	
	/* Print all things: */
	for(i=0; GENERATOR_LIST[i].id; i++) {
		gamelib_print("%s  ", GENERATOR_LIST[i].id);
		put_chars(max_id - strlen(GENERATOR_LIST[i].id), ' ');
		gamelib_print("%s%s\n", GENERATOR_LIST[i].desc, i==0 ? " (Default)":"");
	}
	gamelib_print("\n");
}

