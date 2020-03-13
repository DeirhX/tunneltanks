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
#include "exceptions.h"

/* The constructor sets the video mode: */
Screen::Screen(bool is_fullscreen)
	: is_fullscreen(is_fullscreen)
{
	this->Resize({ SCREEN_WIDTH, SCREEN_HEIGHT });
}


/* Fills a surface with a blue/black pattern: */
void Screen::FillBackground() {
	Size dim = gamelib_get_resolution();
	
	gamelib_draw_box({{ 0, 0}, dim}, Palette.Get(Colors::Background));
	Position o;
	for(o.y = 0; o.y<dim.y; o.y++) {
		for(o.x = (o.y%2)*2; o.x<dim.x; o.x+=4) {
			gamelib_draw_box(Rect{ o, Size {1,1} }, Palette.Get(Colors::BackgroundDot));
		}
	}
}

void Screen::DrawPixel(ScreenPosition pos, Color color) {

	Offset adjusted_size = {  /* Make some pixels uniformly larger to fill in given space relatively evenly  */
		(pos.x * this->pixels_skip.x) / GAME_WIDTH,
		(pos.y * this->pixels_skip.y) / GAME_HEIGHT };
	Offset adjusted_next = {
		((pos.x + 1) * this->pixels_skip.x) / GAME_WIDTH,
		((pos.y + 1) * this->pixels_skip.y) / GAME_HEIGHT };
	
	/* Final pixel position, adjusted by required scaling and offset */ 
	pos.x = (pos.x * this->pixel_size.x) + this->screen_offset.x + adjusted_size.x;
	pos.y = (pos.y * this->pixel_size.y) + this->screen_offset.y + adjusted_size.y;
	
	auto pixelSize = Size { /* Compute size based on needing uneven scaling or not */
		this->pixel_size.x + (adjusted_size.x != adjusted_next.x),
		this->pixel_size.y + (adjusted_size.y != adjusted_next.y)
	};
	
	gamelib_draw_box(Rect{ static_cast<Position>(pos), pixelSize }, color);
}


Position Screen::ScreenToWorld(ScreenPosition screen_pos) {

	Position pos = (Position)screen_pos;
	pos.x -= this->screen_offset.x;
	pos.x -= pos.x/(int)this->pixel_size.x * (int)this->pixels_skip.x / GAME_WIDTH;
	pos.x /= (int)this->pixel_size.x;

	pos.y -= this->screen_offset.y;
	pos.y -= pos.y / (int)this->pixel_size.y * (int)this->pixels_skip.y / GAME_HEIGHT;
	pos.y /= (int)this->pixel_size.y;
	
	return pos;
}

void Screen::DrawStatic(Window *w) {
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
				this->DrawPixel( { x + w->r.pos.x, y + w->r.pos.y }, Palette.GetPrimary(TankColor(Random.Int(0,7))));
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
			color = drawing_black ? Palette.Get(Colors::Blank) : Palette.GetPrimary(TankColor(Random.Int(0, 7)));
			this->DrawPixel({ x + w->r.pos.x, y + w->r.pos.y }, color);
		}
}

#undef _RAND_COLOR
#undef _BLACK_BAR_RAND

/* Will draw a window using the level's drawbuffer: */
void Screen::DrawWindow(Window *w) {
	DrawBuffer *b = this->drawBuffer;

	Position pos = w->t->GetPosition();
	
	for(int y=0; y < w->r.size.y; y++) {
		for(int x=0; x < w->r.size.x; x++) {
			int screen_x = x + w->r.pos.x, screen_y = y + w->r.pos.y;

			Color c = b->GetPixel(Position{ x + pos.x - w->r.size.x / 2, y + pos.y - w->r.size.y / 2 });
			this->DrawPixel({ screen_x, screen_y }, c);
		}
	}
	
	this->DrawStatic(w);
}

/* Will draw two bars indicating the charge/health of a tank: */
/* TODO: This currently draws every frame. Can we make a dirty flag, and only
 *       redraw when it's needed? Also, can we put some of these calculations in
 *       the StatusBar structure, so they don't have to be done every frame? */
void Screen::DrawStatus(StatusBar *b) {
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
			if ((x == 0 || x == b->r.size.x - 1) && (y == 0 || y == b->r.size.y - 1))
				continue;

			/* Outer border draws background: */
			else if (y < STATUS_BORDER || y >= b->r.size.y - STATUS_BORDER ||
				x < STATUS_BORDER || x >= b->r.size.x - STATUS_BORDER)
				c = Palette.Get(Colors::StatusBackground);

			/* We round the corners here a little bit too: */
			else if ((x == STATUS_BORDER || x == b->r.size.x - STATUS_BORDER - 1) &&
				(y == STATUS_BORDER || y == b->r.size.y - STATUS_BORDER - 1))
				c = Palette.Get(Colors::StatusBackground);

			/* Middle seperator draws as backround, as well: */
			else if (y >= mid_y && y < mid_y + mid_h)
				c = Palette.Get(Colors::StatusBackground);

			/* Ok, this must be one of the bars. */
			/* Is this the filled part of the energy bar? */
			else if (y < mid_y &&
				((b->decreases_to_left && x < energy_filled) ||
				(!b->decreases_to_left && x >= energy_filled)))
				c = Palette.Get(Colors::StatusEnergy);

			/* Is this the filled part of the health bar? */
			else if(y > mid_y && 
				(( b->decreases_to_left && x< health_filled) ||
				 (!b->decreases_to_left && x>=health_filled)))
				c = Palette.Get(Colors::StatusHealth);

			/* Else, this must be the empty part of a bar: */
			else
				c = Palette.Get(Colors::Blank);

			this->DrawPixel({ x + b->r.pos.x, y + b->r.pos.y }, c);
		}
	}
}

void Screen::DrawBitmap(Bitmap *b) {
	int x, y, i;

	for(x=y=i=0; i < (b->r.size.x * b->r.size.y); i++) {
		if (b->data[i]) this->DrawPixel({ x + b->r.pos.x, y + b->r.pos.y }, b->color);
		if(++x >= b->r.size.x) { y++; x=0; }
	}
}

void Screen::DrawLevel() {
	int i;
	
	for(i=0; i < this->window_count; i++) DrawWindow(&this->window[i]);
	for(i=0; i < this->status_count; i++) DrawStatus(&this->status[i]);
	for(i=0; i < this->bitmap_count; i++) DrawBitmap(&this->bitmap[i]);
	if(this->controller_count)
		gamelib_gui_draw(this, this->controller.r);
}

void Screen::DrawCurrentMode() {	
	if(this->mode == SCREEN_DRAW_LEVEL) {
		this->DrawLevel();
	}
	//throw GameException("Invalid mode to draw");
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
		this->screen_offset.x = 0;
		this->screen_offset.y = (size.y - render_size.y)/2;
	}
	
	/* Calculate the pixel sizing variables: */
	this->pixel_size.x = render_size.x / GAME_WIDTH;
	this->pixel_size.y = render_size.y / GAME_HEIGHT;
	this->pixels_skip.x = render_size.x % GAME_WIDTH;
	this->pixels_skip.y = render_size.y % GAME_HEIGHT;
	
	/* Draw a nice bg: */
	Screen::FillBackground();
	
	this->screen_size = size;
	
	/* Redraw the game: */
	this->DrawCurrentMode();
}

/* Set the current drawing mode: */
void Screen::SetLevelDrawMode(DrawBuffer *b) {
	this->mode = SCREEN_DRAW_LEVEL;
	this->drawBuffer = b;
}

/*
void Screen::set_mode_menu( Menu *m) ;
void Screen::set_mode_map( Map *m) ;
*/

/* Window creation should only happen in Level-drawing mode: */
void Screen::AddWindow( Rect r, Tank *t) {
	if(this->mode != SCREEN_DRAW_LEVEL) return;
	
	if(this->window_count >= SCREEN_MAX_WINDOWS) return;
	this->window[ this->window_count++ ] = Window {r, t, 0, 0};
}

/* We can add the health/energy status bars here: */
void Screen::AddStatus(Rect r, Tank *t, int decreases_to_left) {
	/* Verify that we're in the right mode, and that we have room: */
	if(this->mode != SCREEN_DRAW_LEVEL) return;
	if(this->status_count >= SCREEN_MAX_STATUS) return;

	/* Make sure that this status bar isn't too small: */
	if(r.size.x <= 2 || r.size.y <= 4) return;
	
	this->status[ this->status_count++ ] = StatusBar {r, t, decreases_to_left};
}

/* We tell the graphics system about GUI graphics here: 
 * 'color' has to be an ADDRESS of a color, so it can monitor changes to the
 * value, especially if the bit depth is changed... 
 * TODO: That really isn't needed anymore, since we haven't cached mapped RGB
 *       values since the switch to gamelib... */
void Screen::AddBitmap( Rect r, char *new_bitmap, Color color) {
	/* Bitmaps are only for game mode: */
	if(this->mode != SCREEN_DRAW_LEVEL) return;
	if(this->bitmap_count >= SCREEN_MAX_BITMAPS) return;
	if(!new_bitmap) return;
	
	this->bitmap[ this->bitmap_count++ ] = Bitmap{r, new_bitmap, color};
}

/* We don't check to see if gamelib needs the gui controller thing in this file.
 * That is handled in game.c: */
void Screen::AddController( Rect r) {
	if(this->mode != SCREEN_DRAW_LEVEL) return;
	if(this->controller_count) return;
	
	this->controller_count = 1;
	this->controller.r = r;
}

