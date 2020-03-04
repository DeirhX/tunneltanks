#ifndef _LEVEL_GEN_UTIL_H_
#define _LEVEL_GEN_UTIL_H_

#include <level.h>
#include <types.h>

void     fill_all  (Level *lvl, char c) ;
void     rough_up(Level *lvl) ;
Vector   pt_rand   (int w, int h, int border) ;
int pt_dist   (Vector a, Vector b) ;
void     set_circle(Level *lvl, int x, int y, char value) ;
void     draw_line (Level *dest, Vector a, Vector b, char value, int fat_line) ;

#endif /* _LEVEL_GEN_UTIL_H_ */

