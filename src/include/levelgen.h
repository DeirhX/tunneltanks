#pragma once
#include <cstdio>
#include <chrono>
#include <level.h>

/* Every level generator needs to conform to this definition: */
typedef void (*LevelGeneratorFunc)(Level *lvl);

/* Generate a level based on an id: */
std::chrono::milliseconds generate_level(Level *lvl, const char *id) ;
void print_levels(FILE *out) ;



