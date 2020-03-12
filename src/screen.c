#include <cstdlib>

#include <screen.h>
#include <tweak.h>
#include <memalloc.h>
#include <random.h>
#include <level.h>
#include <tank.h>
#include <types.h>
#include <tanksprites.h>
#include <drawbuffer.h>
#include <gamelib.h>



/* Fills a surface with a blue/black pattern: */
static void fill_background() {
	Size dim = gamelib_get_resolution();
	
	gamelib_draw_box({{ 0, 0}, dim}, color_bg);
	Position o;
	for(o.y = 0; o.y<dim.y; o.y++) {
		for(o.x = (o.y%2)*2; o.x<dim.x; o.x+=4) {
			gamelib_draw_box(Rect{ o, Size {1,1} }, color_bg_dot);
		}
	}
}

void screen_draw_pixel(Screen* s, Position pos, Color color) {

	Offset adjusted_size = {  /* Make some pixels uniformly larger to fill in given space relatively evenly  */
		(pos.x * s->pixels_skip.x) / GAME_WIDTH,
		(pos.y * s->pixels_skip.y) / GAME_HEIGHT };
	Offset adjusted_next = {
		((pos.x + 1) * s->pixels_skip.x) / GAME_WIDTH,
		((pos.y + 1) * s->pixels_skip.y) / GAME_HEIGHT };
	
	/* Final pixel position, adjusted by required scaling and offset */ 
	pos.x = (pos.x * s->pixel_size.x) + s->screen_offset.x + adjusted_size.x;
	pos.y = (pos.y * s->pixel_size.y) + s->screen_offset.y + adjusted_size.y;
	
	auto pixelSize = Size { /* Compute size based on needing uneven scaling or not */
		s->pixel_size.x + (adjusted_size.x != adjusted_next.x),
		s->pixel_size.y + (adjusted_size.y != adjusted_next.y)
	};
	
	gamelib_draw_box(Rect{ pos, pixelSize }, color);
}

/* These will say what virtual pixel a physical pixel resides on: */
int  screen_map_x(Screen *s, int x) {
	x -= s->screen_offset.x;
	x -= x/(int)s->pixel_size.x * (int)s->pixels_skip.x /GAME_WIDTH;
	x /= (int)s->pixel_size.x;
	
	return x;
}

int  screen_map_y(Screen *s, int y) {
	y -= s->screen_offset.y;
	y -= y/(int)s->pixel_size.y * (int)s->pixels_skip.y /GAME_HEIGHT;
	y /= (int)s->pixel_size.y;
	
	return y;
}


/* Will randomly draw static to a window, based on a tank's health. Returns 1 if
 * static was drawn: */
static void screen_draw_static(Screen *s, Window *w) {
	int x, y;
	//int health = w->t->GetHealth();
    int energy = w->t->GetEnergy();
	int black_counter, drawing_black;
	
	/* Don't do static if we have a lot of energy: */
	if(energy > STATIC_THRESHOLD) {
		w->counter = w->showing_static = 0;
		return;
	}

	if(!w->counter) {
		int intensity = 1000 * energy / STATIC_THRESHOLD;
		w->showing_static = !Random.Bool(intensity);
		w->counter = Random.Int(GAME_FPS/16, GAME_FPS/8) * w->showing_static ? 1u : 4u;

	} else w->counter--;
	
	if(!w->showing_static) return;

#define _BLACK_BAR_RAND Random.Int(1, w->r.size.x*w->r.size.y * STATIC_BLACK_BAR_SIZE/1000)
#define _RAND_COLOR     color_primary[Random.Int(0,7)]

	/* Should we draw a black bar in the image? */
	black_counter = Random.Bool(STATIC_BLACK_BAR_ODDS) ? _BLACK_BAR_RAND : 0;
	drawing_black = black_counter && Random.Bool(STATIC_BLACK_BAR_ODDS);
	
	/* Develop a static thing image for the window: */
	for(y=0; y<w->r.size.y; y++)
		for(x=0; x<w->r.size.x; x++) {
			Color color;

			if(!energy) {
				screen_draw_pixel(s, { x + w->r.pos.x, y + w->r.pos.y }, _RAND_COLOR);
				continue;
			}

			/* Handle all of the black bar logic: */
			if(black_counter) {
				black_counter--;
				if(!black_counter) {
					black_counter = _BLACK_BAR_RAND;
					drawing_black = !drawing_black;
				}
			}

			/* Make this semi-transparent: */
			if(Random.Bool(STATIC_TRANSPARENCY)) continue;

			/* Finally, select a color (either black or random) and draw: */
			color = drawing_black ? color_blank : _RAND_COLOR;
			screen_draw_pixel(s, { x + w->r.pos.x, y + w->r.pos.y }, color);
		}

	return;
}

#undef _RAND_COLOR
#undef _BLACK_BAR_RAND

/* Will draw a window using the level's drawbuffer: */
static void screen_draw_window(Screen *s, Window *w) {
	DrawBuffer *b = s->drawBuffer;

	Position pos = w->t->GetPosition();
	
	for(int y=0; y < w->r.size.y; y++) {
		for(int x=0; x < w->r.size.x; x++) {
			int screen_x = x + w->r.pos.x, screen_y = y + w->r.pos.y;

			Color c = b->GetPixel(Position{ x + pos.x - w->r.size.x / 2, y + pos.y - w->r.size.y / 2 });
			screen_draw_pixel(s, { screen_x, screen_y }, c);
		}
	}
	
	screen_draw_static(s, w);
}

/* Will draw two bars indicating the charge/health of a tank: */
/* TODO: This currently draws every frame. Can we make a dirty flag, and only
 *       redraw when it's needed? Also, can we put some of these calculations in
 *       the StatusBar structure, so they don't have to be done every frame? */
static void screen_draw_status(Screen *s, StatusBar *b) {
	int x, y;
	
	/* At what y value does the median divider start: */
	int mid_y = (b->r.size.y - 1) / 2;
	
	/* How many pixels high is the median divider: */
	int mid_h = (b->r.size.y % 2) ? 1u : 2u;
	
	/* How many pixels are filled in? */
	int energy_filled = b->t->GetEnergy();
    int health_filled = b->t->GetHealth();
    int half_energy_pixel = TANK_STARTING_FUEL/((b->r.size.x - STATUS_BORDER*2)*2);
	
	energy_filled += half_energy_pixel;
	
	energy_filled *= (b->r.size.x - STATUS_BORDER*2);
	energy_filled /= TANK_STARTING_FUEL;
	health_filled *= (b->r.size.x - STATUS_BORDER*2);
	health_filled /= TANK_STARTING_SHIELD;

	/* If we are decreasing to the right, we need to invert those values: */
	if(!b->decreases_to_left) {
		energy_filled = b->r.size.x - STATUS_BORDER - energy_filled;
		health_filled = b->r.size.x - STATUS_BORDER - health_filled;
		
	/* Else, we still need to shift it to the right by STATUS_BORDER: */
	} else {
		energy_filled += STATUS_BORDER;
		health_filled += STATUS_BORDER;
	}
	
	/* Ok, lets draw this thing: */
	for(y=0; y < b->r.size.y; y++) {
		for(x=0; x < b->r.size.x; x++) {
			Color c;

			/* We round the corners of the status box: */
			if((x == 0 || x == b->r.size.x - 1) && (y == 0 || y == b->r.size.y - 1))
				continue;
			
			/* Outer border draws background: */
			else if(y < STATUS_BORDER || y >= b->r.size.y-STATUS_BORDER ||
			   x < STATUS_BORDER || x >= b->r.size.x-STATUS_BORDER)
				c = color_status_bg;

			/* We round the corners here a little bit too: */
			else if((x == STATUS_BORDER || x == b->r.size.x - STATUS_BORDER - 1) &&
			        (y == STATUS_BORDER || y == b->r.size.y - STATUS_BORDER - 1))
				c = color_status_bg;

			/* Middle seperator draws as backround, as well: */
			else if(y >= mid_y && y < mid_y + mid_h)
				c = color_status_bg;

			/* Ok, this must be one of the bars. */
			/* Is this the filled part of the energy bar? */
			else if(y < mid_y && 
				(( b->decreases_to_left && x< energy_filled) ||
				 (!b->decreases_to_left && x>=energy_filled)))
				c = color_status_energy;

			/* Is this the filled part of the health bar? */
			else if(y > mid_y && 
				(( b->decreases_to_left && x< health_filled) ||
				 (!b->decreases_to_left && x>=health_filled)))
				c = color_status_health;

			/* Else, this must be the empty part of a bar: */
			else
				c = color_blank;

			screen_draw_pixel(s, { x + b->r.pos.x, y + b->r.pos.y }, c);
		}
	}
}

static void screen_draw_bitmap(Screen *s, Bitmap *b) {
	int x, y, i;

	for(x=y=i=0; i < (b->r.size.x * b->r.size.y); i++) {
		if (b->data[i]) screen_draw_pixel(s, { x + b->r.pos.x, y + b->r.pos.y }, *b->color);
		if(++x >= b->r.size.x) { y++; x=0; }
	}
}

static void screen_draw_level(Screen *s) {
	int i;
	
	for(i=0; i<s->window_count; i++) screen_draw_window(s, &s->window[i]);
	for(i=0; i<s->status_count; i++) screen_draw_status(s, &s->status[i]);
	for(i=0; i<s->bitmap_count; i++) screen_draw_bitmap(s, &s->bitmap[i]);
	if(s->controller_count)
		gamelib_gui_draw(s, s->controller.r);
}

void screen_draw(Screen *s) {	
	if(s->mode == SCREEN_DRAW_LEVEL) {
		screen_draw_level(s);
	}
}


/* The constructor sets the video mode: */
Screen::Screen (bool is_fullscreen)
: is_fullscreen(is_fullscreen), mode(SCREEN_DRAW_INVALID)
{
	this->Resize({ SCREEN_WIDTH, SCREEN_HEIGHT });
}

/* TODO: Change the screen API to better match gamelib... */

void Screen::SetFullscreen(bool new_fullscreen) {
	
	if(this->is_fullscreen == new_fullscreen) return;
	
	this->is_fullscreen = new_fullscreen;
	
	/* Resize the screen to include the new fullscreen mode: */
	if (!is_fullscreen) this->Resize(Size{ SCREEN_WIDTH, SCREEN_HEIGHT });
	else                this->Resize(this->screen_size);
}


/* Returns if successful */
void Screen::Resize(Size size)
{
	Size render_size;
	this->pixels_skip = {};
	this->screen_offset = {};
	this->pixel_size = {};

	/* Make sure that we aren't scaling to something too small: */
	size.x = std::max(GAME_WIDTH, size.x);
	size.y = std::max(GAME_HEIGHT, size.y);
	
	/* A little extra logic for fullscreen: */
	if(this->is_fullscreen) gamelib_set_fullscreen();
	else                    gamelib_set_window    (size);
	
	size = gamelib_get_resolution();
	
	this->is_fullscreen = gamelib_get_fullscreen();
	
	/* What is the limiting factor in our scaling to maintain aspect ratio? */
	int yw = size.y * GAME_WIDTH; 
	int xh = size.x * GAME_HEIGHT;
	if( yw < xh ) {
		/* size.y is. Correct aspect ratio using offset */
		render_size.x = (GAME_WIDTH * size.y) / (GAME_HEIGHT);
		render_size.y = size.y;
		this->screen_offset.x = (size.x - render_size.x)/2;
		this->screen_offset.y = 0;
	} else {
		/* size.x is. Correct aspect ratio using offset */
		render_size.x = size.x;
		render_size.y = (GAME_HEIGHT * size.x) / (GAME_WIDTH);
		screen_offset.x = 0;
		screen_offset.y = (size.y - render_size.y)/2;
	}
	
	/* Calculate the pixel sizing variables: */
	pixel_size.x = render_size.x / GAME_WIDTH;  pixels_skip.x = render_size.x % GAME_WIDTH;
	pixel_size.y = render_size.y / GAME_HEIGHT; pixels_skip.y = render_size.y % GAME_HEIGHT;
	
	/* Draw a nice bg: */
	fill_background();
	
	this->screen_size = size;
	
	/* Redraw the game: */
	screen_draw(this);
}

/* Set the current drawing mode: */
void Screen::SetLevelDrawMode(DrawBuffer *b) {
	this->mode = SCREEN_DRAW_LEVEL;
	this->drawBuffer = b;
}

/*
void screen_set_mode_menu(Screen *s, Menu *m) ;
void screen_set_mode_map(Screen *s, Map *m) ;
*/

/* Window creation should only happen in Level-drawing mode: */
void screen_add_window(Screen *s, Rect r, Tank *t) {
	if(s->mode != SCREEN_DRAW_LEVEL) return;
	
	if(s->window_count >= SCREEN_MAX_WINDOWS) return;
	s->window[ s->window_count++ ] = Window {r, t, 0, 0};
}

/* We can add the health/energy status bars here: */
void screen_add_status(Screen *s, Rect r, Tank *t, int decreases_to_left) {
	/* Verify that we're in the right mode, and that we have room: */
	if(s->mode != SCREEN_DRAW_LEVEL) return;
	if(s->status_count >= SCREEN_MAX_STATUS) return;

	/* Make sure that this status bar isn't too small: */
	if(r.size.x <= 2 || r.size.y <= 4) return;
	
	s->status[ s->status_count++ ] = StatusBar {r, t, decreases_to_left};
}

/* We tell the graphics system about GUI graphics here: 
 * 'color' has to be an ADDRESS of a color, so it can monitor changes to the
 * value, especially if the bit depth is changed... 
 * TODO: That really isn't needed anymore, since we haven't cached mapped RGB
 *       values since the switch to gamelib... */
void screen_add_bitmap(Screen *s, Rect r, char *bitmap, Color *color) {
	/* Bitmaps are only for game mode: */
	if(s->mode != SCREEN_DRAW_LEVEL) return;
	if(s->bitmap_count >= SCREEN_MAX_BITMAPS) return;
	if(!bitmap || !color) return;
	
	s->bitmap[ s->bitmap_count++ ] = Bitmap{r, bitmap, color};
}

/* We don't check to see if gamelib needs the gui controller thing in this file.
 * That is handled in game.c: */
void screen_add_controller(Screen *s, Rect r) {
	if(s->mode != SCREEN_DRAW_LEVEL) return;
	if(s->controller_count) return;
	
	s->controller_count = 1;
	s->controller.r = r;
}

