#include <cstdio>
#include <cstring>
#include <chrono>
#include <gamelib.h>
#include <levelgen.h>
#include <algorithm>


struct LevelGeneratorDesc {
	LevelGenerator     id;
	const char*        name;
	LevelGeneratorFunc gen;
	const char         *desc;
};


/* === All the generator headers go here: =================================== */

#include <levelgentoast.h>
#include <levelgensimple.h>
#include <levelgenmaze.h>
#include <levelgenbraid.h>
#include <trace.h>

/* Add an entry for every generator: */
std::array<LevelGeneratorDesc, 4> LevelGenerators =
{
	LevelGeneratorDesc{LevelGenerator::Toast,  "toast", levelgen::toast::toast_generator,  "Twisty, cavernous maps."},
	LevelGeneratorDesc{LevelGenerator::Braid,  "braid", levelgen::braid::braid_generator,  "Maze-like maps with no dead ends." },
	LevelGeneratorDesc{LevelGenerator::Maze,   "maze", levelgen::maze::maze_generator,   "Complicated maps with a maze surrounding the bases."},
	LevelGeneratorDesc{LevelGenerator::Simple, "simple", levelgen::simple::simple_generator, "Simple rectangular maps with ragged sides."},
	
};

LevelGenerator GeneratorFromName(const char* name)
{
	if (name)
	{
		/* Look for the id: */
		for (auto& generator : LevelGenerators)
		{
			if (!strcmp(name, generator.name)) {
				return generator.id;
			}
		}
	}
	return LevelGenerator::None;
}


/* ========================================================================== */

/* Linear search is ok here, since there aren't many level generators: */
std::chrono::milliseconds generate_level(Level *lvl, LevelGenerator generator) {
	
	/* If 'id' is null, go with the default: */
	if(generator == LevelGenerator::None) 
		generator = LevelGenerators[0].id;

	auto found = std::find_if(LevelGenerators.begin(), LevelGenerators.end(), [generator](const auto& desc) {return desc.id == generator; });
	if (found == LevelGenerators.end())
	{
		/* Report what level generator we found: */
		gamelib_print("Using default level generator: '%s'\n", LevelGenerators[0].id);
	}
	gamelib_print("Using level generator: '%s'\n", found->name);
	LevelGeneratorFunc func = found->gen;

	{
		Stopwatch<std::chrono::milliseconds> s;

		/* Ok, now generate the level: */
		func(lvl);

		gamelib_print("Level generated in: ");
		auto msecs = s.GetElapsed();
		gamelib_print("%lld.%03lld sec\n", msecs.count() / 1000, msecs.count() % 1000);

		return { std::chrono::duration_cast<std::chrono::milliseconds>(msecs)};
	}
}

/* Will print a specified number of spaces to the file: */
static void put_chars(size_t i, char c) {
	while( i-- )
		gamelib_print("%c", c);
}

void print_levels(FILE *out) {
	size_t max_id = 7;
	size_t max_desc = strlen("Description:");
	
	/* Get the longest ID/Description length: */
	for (auto& generator : LevelGenerators)
	{
		max_id = std::max(max_id, strlen(generator.name));
		max_desc = std::max(max_desc, strlen(generator.desc));
	}
	
	/* Print the header: */
	gamelib_print("ID:  ");
	put_chars(max_id - strlen("ID:"), ' ');
	gamelib_print("Description:\n");
	put_chars(max_id + max_desc + 2, '-');
	gamelib_print("\n");
	
	/* Print all things: */
	for (auto i = 0u; i < LevelGenerators.size(); i++) {
		gamelib_print("%s  ", LevelGenerators[i].name);
		put_chars(max_id - strlen(LevelGenerators[i].name), ' ');
		gamelib_print("%s%s\n", LevelGenerators[i].desc, i==0 ? " (Default)":"");
	}
	gamelib_print("\n");
}

