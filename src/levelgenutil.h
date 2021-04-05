#pragma once
#include <Terrain.h>
#include <types.h>

void rough_up(Terrain * lvl);
Position generate_inside(Size size, int border);
int pt_dist(Vector a, Vector b);
void set_circle(Terrain * lvl, int x, int y, TerrainPixel value);
void draw_line(Terrain * dest, Vector a, Vector b, TerrainPixel value, int fat_line);

void fill_all(Terrain * lvl, TerrainPixel c);
void invert_all(Terrain * lvl);
void unmark_all(Terrain * lvl);
