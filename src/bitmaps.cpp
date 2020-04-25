#include "bitmaps.h"
#include "color.h"
#include "color_palette.h"
#include "screen.h"

template <typename DataType>
template <typename GetColorFunc>
void Bitmap<DataType>::Draw(Screen *screen, ScreenPosition position, GetColorFunc GetPixelColor)
{
    int x = 0;
    int y = 0;
    for (int i = 0; i < (this->GetSize().x * this->GetSize().y); i++)
    {
        screen->DrawPixel({x + position.x, y + position.y}, GetPixelColor(i));

        /* Begin a new line if we're at the...wait for it... end-of-the-line! */
        if (++x >= this->GetSize().x)
        {
            y++;
            x = 0;
        }
    }
}

/* Draw portion of bitmap */
template <typename DataType>
template <typename GetColorFunc>
void Bitmap<DataType>::Draw(Screen * screen, ScreenPosition screen_pos, ImageRect source_rect,
                            GetColorFunc GetPixelColor) /* return Color */
{
    for (int x = source_rect.Left(); x <= source_rect.Right(); ++x)
        for (int y = source_rect.Top(); y <= source_rect.Bottom(); ++y)
        {
            /* Draw its color or transparent nothing if it's a black/white bitmap */
            screen->DrawPixel({x - source_rect.Left() + screen_pos.x, y - source_rect.Top() + screen_pos.y},
                              GetPixelColor(this->ToIndex({x, y})));
        }
}

/* Draw portion of bitmap into a screen rectangle, clipping it if it exceeds bounds */
template <typename DataType>
template <typename GetColorFunc>
void Bitmap<DataType>::Draw(Screen * screen, ScreenRect screen_rect, ImageRect source_rect,
                            GetColorFunc GetPixelColor) /* return Color */
{
    auto actual_width = std::min(source_rect.size.x, screen_rect.size.x);
    auto actual_height = std::min(source_rect.size.y, screen_rect.size.y);
    for (int x = 0; x < actual_width ; ++x)
        for (int y = 0; y < actual_height; ++y)
        {
            /* Draw its color or transparent nothing if it's a black/white bitmap */
            screen->DrawPixel({x + screen_rect.Left(), y + screen_rect.Top()},
                              GetPixelColor(this->ToIndex({x + source_rect.Left(), y + source_rect.Top()})));
        }
}

void MonoBitmap::Draw(Screen * screen, ScreenPosition screen_pos, Color color)
{
    Base::Draw(screen, screen_pos,
               [this, color](int index) { return this->At(index) ? color : 
                    Palette.Get(Colors::Transparent); });
}
void MonoBitmap::Draw(Screen * screen, ScreenPosition screen_pos, ImageRect source_rect, Color color)
{
    Base::Draw(screen, screen_pos, source_rect,
               [this, color](int index) { return this->At(index) ? color : 
                    Palette.Get(Colors::Transparent); });
}

void MonoBitmap::Draw(Screen * screen, ScreenRect screen_rect, ImageRect source_rect, Color color)
{
    Base::Draw(screen, screen_rect, source_rect,
               [this, color](int index) { return this->At(index) ? color : Palette.Get(Colors::Transparent); });
}

        void ColorBitmap::Draw(Screen * screen, ScreenPosition screen_pos)
{
    Base::Draw(screen, screen_pos, 
               [this](int index) { return this->At(index); });
}
void ColorBitmap::Draw(Screen * screen, ScreenPosition screen_pos, Color color_filter)
{
    Base::Draw(screen, screen_pos, 
               [this, color_filter](int index) { return color_filter.Mask(this->At(index)); });
}

void ColorBitmap::Draw(Screen * screen, ScreenPosition screen_pos, ImageRect source_rect)
{
    Base::Draw(screen, screen_pos, source_rect, 
               [this](int index) { return this->At(index); });
}
void ColorBitmap::Draw(Screen * screen, ScreenRect screen_rect, ImageRect source_rect)
{
    Base::Draw(screen, screen_rect, source_rect, [this](int index) { return this->At(index); });
}
void ColorBitmap::Draw(Screen * screen, ScreenPosition screen_pos, ImageRect source_rect, Color color_filter)
{
    Base::Draw(screen, screen_pos, source_rect,
               [this, color_filter](int index) { return color_filter.Mask(this->At(index)); });
}
void ColorBitmap::Draw(Screen * screen, ScreenRect screen_rect, ImageRect source_rect, Color color_filter)
{
    Base::Draw(screen, screen_rect, source_rect,
               [this, color_filter](int index) { return color_filter.Mask(this->At(index)); });
}
