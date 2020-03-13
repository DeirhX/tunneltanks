#pragma once
#include <cstdio>
#include <chrono>
#include <level.h>

enum class LevelGenerator
{
	None,
	Toast,
	Braid,
	Maze,
	Simple,
};

LevelGenerator GeneratorFromName(const char* name);

/* Every level generator needs to conform to this definition: */
typedef void (*LevelGeneratorFunc)(Level *lvl);

/* Generate a level based on an id: */
std::chrono::milliseconds generate_level(Level *lvl, LevelGenerator id) ;
void print_levels(FILE *out) ;

