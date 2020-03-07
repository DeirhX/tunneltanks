#pragma once
#include <cstdio>

#include <level.h>

/* Every level generator needs to conform to this definition: */
typedef void (*LevelGeneratorFunc)(Level *lvl);

/* Generate a level based on an id: */
void generate_level(Level *lvl, const char *id) ;
void print_levels(FILE *out) ;



