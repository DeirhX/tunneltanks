#ifndef _SCREEN_H_
#define _SCREEN_H_

typedef struct Screen Screen;

#include "level.h"
#include "types.h"
#include "tank.h"
#include "drawbuffer.h"


/* (Con|De)structor: */
Screen  *screen_new(int is_fullscreen) ;
void     screen_destroy(Screen *s) ;

/* Resizing the screen: */
/*int  screen_list_fs_resolutions(Screen *s, SDL_Rect **dest, unsigned *count) ;*/
void screen_set_fullscreen(Screen *s, int is_fullscreen) ;
int screen_resize(Screen *s, unsigned width, unsigned height) ;

/* Set the current drawing mode: */
void screen_draw_pixel(Screen *s, unsigned x, unsigned y, Color color) ;

void screen_set_mode_level(Screen *s, DrawBuffer *b) ;
/*
void screen_set_mode_menu(Screen *s, Menu *m) ;
void screen_set_mode_map(Screen *s, Map *m) ;
*/

/* Window creation/removal: */

void screen_add_window(Screen *s, Rect r, Tank *t) ;
void screen_add_status(Screen *s, Rect r, Tank *t, int decreases_to_left) ;
void screen_add_bitmap(Screen *s, Rect r, char *bitmap, Color *color) ;

/*
void     screen_remove_window(Screen *s, WindowID id) ;
*/
/* Draw the structure: */
void screen_flip(Screen *s) ;

#endif /* _SCREEN_H_ */

