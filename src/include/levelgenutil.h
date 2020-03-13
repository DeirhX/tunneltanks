#pragma once
#include <level.h>
#include <types.h>

void     fill_all  (Level *lvl, char c) ;
void     rough_up(Level *lvl) ;
Position  pt_rand   (Size size, int border) ;
int pt_dist   (Vector a, Vector b) ;
void     set_circle(Level *lvl, int x, int y, LevelVoxel value) ;
void     draw_line (Level *dest, Vector a, Vector b, LevelVoxel value, int fat_line) ;

void invert_all(Level* lvl);
