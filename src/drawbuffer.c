#include <drawbuffer.h>
#include <memalloc.h>

/* Various colors for use in the game: */
Color color_dirt_hi       = Color(0xc3, 0x79, 0x30);
Color color_dirt_lo       = Color(0xba, 0x59, 0x04);
Color color_rock          = Color(0x9a, 0x9a, 0x9a);
Color color_fire_hot      = Color(0xff, 0x34, 0x08);
Color color_fire_cold     = Color(0xba, 0x00, 0x00);
Color color_blank         = Color(0x00, 0x00, 0x00);
Color color_bg            = Color(0x00, 0x00, 0x00);
Color color_bg_dot        = Color(0x00, 0x00, 0xb6);
Color color_status_bg     = Color(0x65, 0x65, 0x65);
Color color_status_energy = Color(0xf5, 0xeb, 0x1a);
Color color_status_health = Color(0x26, 0xf4, 0xf2);

/* Primary colors: */
Color color_primary[8] = {
	Color(0x00, 0x00, 0x00),
	Color(0xff, 0x00, 0x00),
	Color(0x00, 0xff, 0x00),
	Color(0xff, 0xff, 0x00),
	Color(0x00, 0x00, 0xff),
	Color(0xff, 0x00, 0xff),
	Color(0x00, 0xff, 0xff),
	Color(0xff, 0xff, 0xff)
};

Color color_tank[8][3] = {
	/* Blue tank: */
	{ Color(0x2c,0x2c,0xff), Color(0x00,0x00,0xb6), Color(0xf3,0xeb,0x1c) },
	
	/* Green tank: */
	{ Color(0x00,0xff,0x00), Color(0x00,0xaa,0x00), Color(0xf3,0xeb,0x1c) },
	
	/* Red tank: */
	{ Color(0xff,0x00,0x00), Color(0xaa,0x00,0x00), Color(0xf3,0xeb,0x1c) },
	
	/* Pink tank: */
	{ Color(0xff,0x99,0x99), Color(0xaa,0x44,0x44), Color(0xf3,0xeb,0x1c) },
	
	/* Purple tank: */
	{ Color(0xff,0x00,0xff), Color(0xaa,0x00,0xaa), Color(0xf3,0xeb,0x1c) },
	
	/* White tank: */
	{ Color(0xee,0xee,0xee), Color(0x99,0x99,0x99), Color(0xf3,0xeb,0x1c) },
	
	/* Aqua tank: */
	{ Color(0x00,0xff,0xff), Color(0x00,0xaa,0xaa), Color(0xf3,0xeb,0x1c) },
	
	/* Gray tank: */
	{ Color(0x66,0x66,0x66), Color(0x33,0x33,0x33), Color(0xf3,0xeb,0x1c) }
};


/* TODO: We're using color structures here because we started with Uint32 values
 *       and this was an easier transition. Eventually, all colors will be in a
 *       central array, and the pixel data will simply be 1-byte indexes. */
struct DrawBuffer {
	Color *pixel_data;
	int w, h;
	Color default_color;
};


DrawBuffer *drawbuffer_new(int w, int h) {
	DrawBuffer *out = get_object(DrawBuffer);
	out->pixel_data = static_cast<Color*>(get_mem(sizeof(Color) * w * h));
	out->w = w; out->h = h;
	out->default_color.r = 0;
	out->default_color.g = 0;
	out->default_color.b = 0;
	
	return out;
}

void drawbuffer_destroy(DrawBuffer *b) {
	if(!b) return;
	free_mem(b->pixel_data);
	free_mem(b);
}

void drawbuffer_set_default(DrawBuffer *b, Color color) {
	b->default_color = color;
}

void drawbuffer_set_pixel(DrawBuffer *b, int x, int y, Color color) {
	if(x >= b->w || y >= b->h) return;
	b->pixel_data[ y * b->w + x ] = color;
}

Color drawbuffer_get_pixel(DrawBuffer *b, int x, int y) {
	if(x < 0 || y < 0 || x >= b->w || y >= b->h) return b->default_color;
	return b->pixel_data[ y * b->w + x ];
}

