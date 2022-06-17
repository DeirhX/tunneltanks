#include "pch.h"
#include "bitmaps.h"
#include "color.h"
#include "color_palette.h"
#include "screen.h"
namespace crust
{
void MonoBitmap::Draw(Surface & surface, ScreenPosition screen_pos, Color color, int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(surface, screen_pos,
               [this, color, spriteOffset](int index)
               { return this->At(index + spriteOffset) ? color : Palette.Get(Colors::Transparent); });
}
void MonoBitmap::Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect, Color color,
                      int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(surface, screen_pos, source_rect,
               [this, color, spriteOffset](int index)
               { return this->At(index + spriteOffset) ? color : Palette.Get(Colors::Transparent); });
}

void MonoBitmap::Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect, Color color, int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(surface, screen_rect, source_rect,
               [this, color, spriteOffset](int index)
               { return this->At(index + spriteOffset) ? color : Palette.Get(Colors::Transparent); });
}

void ColorBitmap::Draw(Surface & surface, ScreenPosition screen_pos, int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(surface, screen_pos, [this, spriteOffset](int index) { return this->At(index + spriteOffset); });
}
void ColorBitmap::Draw(Surface & surface, ScreenPosition screen_pos, Color color_filter, int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(surface, screen_pos,
               [this, spriteOffset, color_filter](int index)
               { return color_filter.Mask(this->At(index + spriteOffset)); });
}

void ColorBitmap::Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect, int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(surface, screen_pos, source_rect,
               [this, spriteOffset](int index) { return this->At(index + spriteOffset); });
}
void ColorBitmap::Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect, int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(
        surface, screen_rect, source_rect, [this, spriteOffset](int index) { return this->At(index + spriteOffset); },
        spriteId);
}
void ColorBitmap::Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect, Color color_filter,
                       int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(
        surface, screen_pos, source_rect,
        [this, spriteOffset, color_filter](int index) { return color_filter.Mask(this->At(index + spriteOffset)); },
        spriteId);
}
void ColorBitmap::Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect, Color color_filter,
                       int spriteId) const
{
    int spriteOffset = int(this->GetSize().Area() * spriteId);
    Base::Draw(
        surface, screen_rect, source_rect,
        [this, spriteOffset, color_filter](int index) { return color_filter.Mask(this->At(index + spriteOffset)); },
        spriteId);
}

// Explicit instantiation
template class Bitmap<std::uint8_t>;

} // namespace crust