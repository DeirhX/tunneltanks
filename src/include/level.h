#pragma once
typedef struct Level Level;

#include <drawbuffer.h>
#include <types.h>


/* (Con|De)structor: */
Level *level_new(DrawBuffer *b, int w, int h) ;
void   level_destroy(Level *lvl) ;

/* Exposes the level data: */
void level_set(Level *lvl, int x, int y, char data) ;
char level_get(Level *lvl, int x, int y) ;

/* Decorates a level for drawing. Should be called by level generators: */
void level_decorate(Level *lvl) ;
void level_make_bases(Level *lvl) ;

Vector level_get_spawn(Level *lvl, int i);

int level_dig_hole(Level *lvl, int x, int y) ;

void level_draw_all(Level *lvl, DrawBuffer *b) ;
void level_draw_pixel(Level *lvl, DrawBuffer *b, int x, int y) ;

/* Will return a value indicating coll: */
typedef enum BaseCollision {
	BASE_COLLISION_NONE,
	BASE_COLLISION_YOURS,
	BASE_COLLISION_ENEMY
} BaseCollision;

BaseCollision level_check_base_collision(Level *lvl, int x, int y, int color) ;

/* Dumps a decorated level into a color bmp file: */
void level_dump_bmp(Level *lvl, const char *filename) ;



