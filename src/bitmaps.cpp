#include "bitmaps.h"
#include "screen.h"
#include "colors.h"

void Bitmap::Draw(Screen* screen, Position pos, Color color)
{
	int x = 0;
	int y = 0;
	for (int i = x = y = 0; i < (this->size.x * this->size.y); i++) {

		/* Draw its color or blank if it's a black/white bitmap */
		auto draw_color = this->At(i) ? color : Palette.Get(Colors::Blank);
		screen->DrawPixel({ x + pos.x, y + pos.y }, draw_color);

		/* Begin a new line if */
		if (++x >= this->size.x)
		{
			y++; x = 0;
		}
	}
}

void Bitmap::Draw(Screen* screen, Position screen_pos, Rect source_rect, Color color)
{
	for(int x = source_rect.Left(); x <= source_rect.Right(); ++x)
		for (int y = source_rect.Top(); y <= source_rect.Bottom(); ++y)
		{
			/* Draw its color or blank if it's a black/white bitmap */
			auto draw_color = this->At(this->ToIndex({x, y})) ? color : Palette.Get(Colors::Blank);
			screen->DrawPixel({ x + screen_pos.x, y + screen_pos.y }, draw_color);
		}
}
