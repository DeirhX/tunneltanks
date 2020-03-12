#pragma once
#include <memory>
#include <types.h>

extern Color color_dirt_hi;
extern Color color_dirt_lo;
extern Color color_rock;
extern Color color_fire_hot;
extern Color color_fire_cold;
extern Color color_blank;
extern Color color_bg;
extern Color color_bg_dot;
extern Color color_status_bg;
extern Color color_status_energy;
extern Color color_status_health;
extern Color color_decal;
extern Color color_primary[8];
extern Color color_tank[8][3];


class DrawBuffer
{
	std::unique_ptr<Color> pixel_data;
	Size size;
	Color default_color;

public:
	DrawBuffer(Size size);
	Color& DefaultColor() { return default_color; }
	void SetDefaultColor(Color color) { default_color = color; }
	void SetPixel(Position pos, Color color);
	Color GetPixel(Position pos);
};

