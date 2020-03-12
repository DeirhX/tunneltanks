#pragma once
struct Screen;

#include <level.h>
#include <types.h>
#include <tank.h>
#include <drawbuffer.h>


typedef enum ScreenDrawMode {
	SCREEN_DRAW_INVALID,
	SCREEN_DRAW_LEVEL
} ScreenDrawMode;

typedef struct Window {
	Rect  r;
	struct Tank* t;
	int counter;
	int showing_static;
} Window;

typedef struct StatusBar {
	Rect  r;
	struct Tank* t;
	int      decreases_to_left;
} StatusBar;

typedef struct Bitmap {
	Rect     r;
	char* data;
	Color* color;
} Bitmap;

typedef struct GUIController {
	Rect r;
} GUIController;


struct Screen {

	bool	 is_fullscreen;

	/* Various variables for the current resolution: */
	Size screen_size = {};
	Offset screen_offset = {};
	Size pixel_size = {};
	Size pixels_skip = {};

	/* Window shit: */
	int  window_count = 0;
	Window    window[SCREEN_MAX_WINDOWS];

	/* Status bar shit: */
	int  status_count = 0;
	StatusBar status[SCREEN_MAX_STATUS];

	/* Bitmap shit: */
	int  bitmap_count = 0;
	Bitmap    bitmap[SCREEN_MAX_BITMAPS];

	/* GUI Controller shit: */
	int controller_count = 0;
	GUIController controller;
	/* Variables used for drawing: */
	ScreenDrawMode mode = SCREEN_DRAW_INVALID;
	DrawBuffer* drawBuffer = nullptr;
public:
	Screen(bool is_fullscreen);

	/* Resizing the screen: */
	bool GetFullscreen() { return is_fullscreen; }
	void SetFullscreen(bool is_fullscreen);
	void Resize(Size size);

	/* Set the current drawing mode: */
	void SetLevelDrawMode(DrawBuffer* b);

};


/*
void screen_set_mode_menu(Screen *s, Menu *m) ;
void screen_set_mode_map(Screen *s, Map *m) ;
*/

/* A few useful functions for external drawing: */
void screen_draw_pixel(Screen *s, Position pos, Color color) ;
int  screen_map_x(Screen *s, int x) ;
int  screen_map_y(Screen *s, int y) ;

/* Window creation/removal: */

void screen_add_window(Screen *s, Rect r, struct Tank *t) ;
void screen_add_status(Screen *s, Rect r, struct Tank *t, int decreases_to_left) ;
void screen_add_bitmap(Screen *s, Rect r, char *bitmap, Color *color) ;
void screen_add_controller(Screen *s, Rect r) ;

/*
void     screen_remove_window(Screen *s, WindowID id) ;
*/
/* Draw the structure: */
void screen_draw(Screen *s) ;



