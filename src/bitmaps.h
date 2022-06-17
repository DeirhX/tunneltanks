#pragma once
#include "color.h"
#include "types.h"
#include <cstddef>
#include "image_data.h"
#include "render_surface.h"

namespace crust
{

/*
 * BitmapTransform : the transformation of top left corner of the bitmap relative to entity's position
 */
class BitmapTransform
{
  public:
    Offset offset{};
};

/*
 * Bitmap : drawable color information
 *          Can contain multiple sprites. If so, the data is expected to contain them sequentially.
 *          Values contained may be looked up using a ColorPalette to assign colors
 */
template <typename DataType>
class Bitmap : public SpriteImageData<DataType>
{
    using Base = SpriteImageData<DataType>;

  public:
    using PixelType = DataType;
    using Base::Base;

  public:
    /* Draw entire bitmap */
    template <typename GetColorFunc>
    void Draw(Surface & surface, ScreenPosition position,
              GetColorFunc GetPixelColor, /* Color GetPixelColor(int index) */
              int spriteId = 0) const;
    /* Draw portion of bitmap */
    template <typename GetColorFunc>
    void Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect,
              GetColorFunc GetPixelColor, /* Color GetPixelColor(int index) */
              int spriteId = 0) const;
    /* Draw portion of bitmap into a screen rectangle, clipping it if it exceeds bounds */
    template <typename GetColorFunc>
    void Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect,
              GetColorFunc GetPixelColor, /* Color GetPixelColor(int index) */
              int spriteId = 0) const;

  public:
    [[nodiscard]] int ToIndex(Position position) const { return position.x + position.y * this->GetSize().x; }
};

template <typename DataType>
template <typename GetColorFunc>
void Bitmap<DataType>::Draw(Surface & surface, ScreenPosition position, GetColorFunc GetPixelColor, int spriteId) const
{
    int x = 0;
    int y = 0;
    for (int i = 0; i < (this->GetSize().x * this->GetSize().y); i++)
    {
        surface.SetPixel({x + position.x, y + position.y}, GetPixelColor(i));

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
void Bitmap<DataType>::Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect,
                            GetColorFunc GetPixelColor, /* return Color */
                            int spriteId) const
{
    for (int x = source_rect.Left(); x <= source_rect.Right(); ++x)
        for (int y = source_rect.Top(); y <= source_rect.Bottom(); ++y)
        {
            /* Draw its color or transparent nothing if it's a black/white bitmap */
            surface.SetPixel({x - source_rect.Left() + screen_pos.x, y - source_rect.Top() + screen_pos.y},
                             GetPixelColor(this->ToIndex({x, y})));
        }
}

/* Draw portion of bitmap into a screen rectangle, clipping it if it exceeds bounds */
template <typename DataType>
template <typename GetColorFunc>
void Bitmap<DataType>::Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect,
                            GetColorFunc GetPixelColor, /* return Color */
                            int spriteId) const
{
    auto actual_width = std::min(source_rect.size.x, screen_rect.size.x);
    auto actual_height = std::min(source_rect.size.y, screen_rect.size.y);
    for (int x = 0; x < actual_width; ++x)
        for (int y = 0; y < actual_height; ++y)
        {
            /* Draw its color or transparent nothing if it's a black/white bitmap */
            surface.SetPixel({x + screen_rect.Left(), y + screen_rect.Top()},
                             GetPixelColor(this->ToIndex({x + source_rect.Left(), y + source_rect.Top()})));
        }
}

/*
 * IndexedBitmap: each byte can represent up to 255 indexable colors
 *                works together with ColorLookup
 */

class IndexedBitmap : public Bitmap<std::uint8_t>
{
    using Base = Bitmap<std::uint8_t>;

  public:
    using Base::Base;
    /* Draw entire bitmap */
    template <typename GetColorFromIndex>
    void Draw(Surface & surface, ScreenPosition screen_pos, GetColorFromIndex colorLookup, int spriteId = 0) const
    {
        int spriteOffset = static_cast<int>(this->GetSize().Area() * spriteId);
        Base::Draw(surface, screen_pos,
                   [this, colorLookup, spriteOffset](int index)
                   { return colorLookup(this->At(index + spriteOffset)); });
    }
    /* Draw a portion of bitmap */
    template <typename GetColorFromIndex>
    void Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect, GetColorFromIndex colorLookup,
              int spriteId = 0) const
    {
        int spriteOffset = static_cast<int>(this->GetSize().Area() * spriteId);
        Base::Draw(surface, screen_pos, source_rect,
                   [this, colorLookup, spriteOffset](int index)
                   { return colorLookup(this->At(index + spriteOffset)); });
    }
    /* Draw a portion of bitmap, possibly clipping to fit into screen rect */
    template <typename GetColorFromIndex>
    void Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect, GetColorFromIndex colorLookup,
              int spriteId = 0) const
    {
        int spriteOffset = static_cast<int>(this->GetSize().Area() * spriteId);
        Base::Draw(surface, screen_rect, source_rect,
                   [this, colorLookup, spriteOffset](int index)
                   { return colorLookup(this->At(index + spriteOffset)); });
    }
};

/*
 * MonoBitmap: a true 'bitmap, mapping one for value and zero for transparency
 */
class MonoBitmap : public Bitmap<std::uint8_t>
{
    using Base = Bitmap<std::uint8_t>;

  public:
    using Base::Base;
    /* Draw entire bitmap */
    void Draw(Surface & surface, ScreenPosition position, Color color, int spriteId = 0) const;
    /* Draw a portion of bitmap */
    void Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect, Color color, int spriteId = 0) const;
    /* Draw a portion of bitmap, possibly clipping to fit into screen rect */
    void Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect, Color color, int spriteId = 0) const;

  private:
    // int ToIndex(Position position) const { return position.x + position.y * size.x; }
};

/*
 * ColorBitmap: full-fledged 32-bit RGBA color data
 *
 */
class ColorBitmap : public Bitmap<Color>
{
    using Base = Bitmap<Color>;

  public:
    using Base::Base;
    /* Draw entire bitmap */
    void Draw(Surface & surface, ScreenPosition screen_pos, int spriteId = 0) const;
    void Draw(Surface & surface, ScreenPosition screen_pos, Color color_filter, int spriteId = 0) const;
    /* Draw portion of bitmap */
    void Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect, int spriteId = 0) const;
    void Draw(Surface & surface, ScreenPosition screen_pos, ImageRect source_rect, Color color_filter,
              int spriteId = 0) const;
    void Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect, int spriteId = 0) const;
    void Draw(Surface & surface, ScreenRect screen_rect, ImageRect source_rect, Color color_filter,
              int spriteId = 0) const;

  private:
    // int ToIndex(Position position) const { return position.x + position.y * size.x; }
};

/* Simple hardcoded bitmaps */
namespace bitmaps
{
    // clang-format off
    inline auto GuiHealth = MonoBitmap(Size{4, 5}, 
    {
        1, 0, 0, 1,
        1, 0, 0, 1,
        1, 1, 1, 1,
        1, 0, 0, 1,
        1, 0, 0, 1
    });

    inline auto GuiEnergy = MonoBitmap(Size{4, 5}, 
    {
        1, 1, 1, 1,
        1, 0, 0, 0,
        1, 1, 1, 0,
        1, 0, 0, 0,
        1, 1, 1, 1
    });

    inline auto LifeDot = MonoBitmap(Size{2, 2}, 
{
         1, 1,
         1, 1,
    });

    inline auto Crosshair = MonoBitmap(Size{3, 3}, 
{
        0, 1, 0,
        1, 1, 1,
        0, 1, 0
    });

    // clang-format on
} // namespace bitmaps

} // namespace crust