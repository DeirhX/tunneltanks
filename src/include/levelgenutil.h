#pragma once
#include <level.h>
#include <types.h>


void     rough_up(Level *lvl) ;
Position  generate_inside   (Size size, int border) ;
int pt_dist   (Vector a, Vector b) ;
void     set_circle(Level *lvl, int x, int y, LevelPixel value) ;
void     draw_line (Level *dest, Vector a, Vector b, LevelPixel value, int fat_line) ;

void fill_all(Level* lvl, LevelPixel c);
void invert_all(Level* lvl);
void unmark_all(Level* lvl);
