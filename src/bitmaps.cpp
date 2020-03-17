#include "bitmaps.h"
#include "screen.h"
#include "colors.h"


template <typename DataType>
template <typename GetColorFunc>
void Bitmap<DataType>::Draw(Screen* screen, Position position, GetColorFunc GetPixelColor)
{
	int x = 0;
	int y = 0;
	for (int i = x = y = 0; i < (this->size.x * this->size.y); i++) {

		screen->DrawPixel({ x + position.x, y + position.y }, GetPixelColor(i));

		/* Begin a new line if */
		if (++x >= this->size.x)
		{
			y++; x = 0;
		}
	}
}

template <typename DataType>
template <typename GetColorFunc>
void Bitmap<DataType>::Draw(Screen* screen, Position screen_pos, Rect source_rect, GetColorFunc GetPixelColor)
{
	for (int x = source_rect.Left(); x <= source_rect.Right(); ++x)
		for (int y = source_rect.Top(); y <= source_rect.Bottom(); ++y)
		{
			/* Draw its color or blank if it's a black/white bitmap */
			screen->DrawPixel({ x + screen_pos.x, y + screen_pos.y }, GetPixelColor(this->ToIndex({ x, y })));
		}
}


void MonoBitmap::Draw(Screen* screen, Position screen_pos, Color color)
{
	Base::Draw(screen, screen_pos, [this, color](int index) { return this->At(index) ? color : Palette.Get(Colors::Blank); });
}
void MonoBitmap::Draw(Screen* screen, Position screen_pos, Rect source_rect, Color color)
{
	Base::Draw(screen, screen_pos, source_rect, [this, color](int index) { return this->At(index) ? color : Palette.Get(Colors::Blank); });
}

void ColorBitmap::Draw(Screen* screen, Position screen_pos)
{
	Base::Draw(screen, screen_pos, [this](int index) { return this->At(index); });
}
void ColorBitmap::Draw(Screen* screen, Position screen_pos, Color color_filter)
{
	Base::Draw(screen, screen_pos, [this, color_filter](int index) { return color_filter.Mask(this->At(index)); });
}

void ColorBitmap::Draw(Screen* screen, Position screen_pos, Rect source_rect)
{
	Base::Draw(screen, screen_pos, source_rect, [this](int index) { return this->At(index); });
}
void ColorBitmap::Draw(Screen* screen, Position screen_pos, Rect source_rect, Color color_filter)
{
	Base::Draw(screen, screen_pos, source_rect, [this, color_filter](int index) { return color_filter.Mask(this->At(index)); });
}

