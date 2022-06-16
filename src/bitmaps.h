#pragma once
#include "color.h"
#include "types.h"
#include <cassert>
#include <cstddef>
#include <initializer_list>
#include <vector>
namespace crust
{

class Screen;

/*
 *  ValueArray: an iterable, generic container for any type of 2D image data.
 *   Can be simple bits, 8-bit values or full-fledged RGB or RGBA data.
 */
template <typename DataType = uint8_t>
class ImageData
{
  public:
    using Container = std::vector<DataType>;
    using iterator = typename Container::iterator;
    using const_iterator = typename Container::const_iterator;
    /* Wasteful to copy in dynamically-allocated memory. But expecting we'll be keeping bitmaps in files in near future
     * when it's gonna be needed. Hang on! */
  protected:
    /* 2-D dimensions of the image*/
    Size size;
    /* The actual data of templated type*/
    Container data;

  public:
    /* In-place value initialization from hardcoded byte-array */
    ImageData(Size size, std::initializer_list<DataType> data) : size(size), data(data)
    {
        assert(size.x * size.y == int(data.size()));
    }
    /* Initialization for dynamic content */
    ImageData(Size size) : size(size) { data.resize(size.x * size.y); }

    size_t GetLength() const { return data.size(); }
    Size GetSize() const { return this->size; }

    /* Read-write accessors */
    DataType & At(int index)
    {
        assert(index >= 0 && index < size.x * size.y);
        return data[index];
    }
    [[nodiscard]] const DataType & At(int index) const
    {
        assert(index >= 0 && index < size.x * size.y);
        return data[index];
    }
    DataType & operator[](int index) { return At(index); }
    [[nodiscard]] const DataType & operator[](int index) const { return At(index); }

    /* Iterator support */
    iterator begin() { return data.begin(); }
    iterator end() { return data.end(); }
    const_iterator cbegin() const { return data.cbegin(); }
    const_iterator cend() const { return data.cend(); }

    /* Conversion to raw data */
    operator Container() const { return data; }
};

template <typename DataType = uint8_t>
class SpriteImageData : public ImageData<DataType>
{
    using Base = ImageData<DataType>;

  public:
    /* In-place value initialization from hardcoded byte-array */
    SpriteImageData(Size size, std::initializer_list<DataType> data, int spriteCount = 1)
        : Base(Size(size.x, size.y * spriteCount), data), spriteCount(spriteCount), spriteSize(size)
    {
        assert(size.Area() == int(data.size()));
    }
    /* Initialization for dynamic content */
    SpriteImageData(Size size, int spriteCount = 1)
        : Base(Size(size.x, size.y * spriteCount)), spriteCount(spriteCount), spriteSize(size)
    {
    }

    Size GetSize() const { return this->spriteSize; }

  private:
    int spriteCount;
    Size spriteSize;
};

template <typename DataType>
class Bitmap : public SpriteImageData<DataType>
{
    using Base = SpriteImageData<DataType>;

  public:
    using PixelType = DataType;
    using Bitmap::Bitmap;

  protected:
    /* Draw entire bitmap */
    template <typename GetColorFunc>
    void Draw(Screen * screen, ScreenPosition position, GetColorFunc GetPixelColor, /* Color GetPixelColor(int index) */
              int spriteId = 0);
    /* Draw portion of bitmap */
    template <typename GetColorFunc>
    void Draw(Screen * screen, ScreenPosition screen_pos, ImageRect source_rect,
              GetColorFunc GetPixelColor, /* Color GetPixelColor(int index) */
              int spriteId = 0);
    /* Draw portion of bitmap into a screen rectangle, clipping it if it exceeds bounds */
    template <typename GetColorFunc>
    void Draw(Screen * screen, ScreenRect screen_rect, ImageRect source_rect,
              GetColorFunc GetPixelColor, /* Color GetPixelColor(int index) */
              int spriteId = 0);

  public:
    [[nodiscard]] int ToIndex(Position position) const { return position.x + position.y * this->GetSize().x; }
};

/*
 * MonoBitmap: a true 'bitmap, mapping one for value and zero for transparency
 * Can contain multiple sprites. If so, the data is expected to contain them sequentially.
 */
class MonoBitmap : public Bitmap<std::uint8_t>
{
    using Base = Bitmap<std::uint8_t>;

  public:
    using Base::Base;
    /* Draw entire bitmap */
    void Draw(Screen * screen, ScreenPosition position, Color color, int spriteId = 0);
    /* Draw a portion of bitmap */
    void Draw(Screen * screen, ScreenPosition screen_pos, ImageRect source_rect, Color color, int spriteId = 0);
    /* Draw a portion of bitmap, possibly clipping to fit into screen rect */
    void Draw(Screen * screen, ScreenRect screen_rect, ImageRect source_rect, Color color, int spriteId = 0);

  private:
    // int ToIndex(Position position) const { return position.x + position.y * size.x; }
};

/* ColorBitmap: full-fledged 32-bit RGBA color data */
class ColorBitmap : public Bitmap<Color>
{
    using Base = Bitmap<Color>;

  public:
    using Base::Base;
    /* Draw entire bitmap */
    void Draw(Screen * screen, ScreenPosition screen_pos, int spriteId = 0);
    void Draw(Screen * screen, ScreenPosition screen_pos, Color color_filter, int spriteId = 0);
    /* Draw portion of bitmap */
    void Draw(Screen * screen, ScreenPosition screen_pos, ImageRect source_rect, int spriteId = 0);
    void Draw(Screen * screen, ScreenPosition screen_pos, ImageRect source_rect, Color color_filter, int spriteId = 0);
    void Draw(Screen * screen, ScreenRect screen_rect, ImageRect source_rect, int spriteId = 0);
    void Draw(Screen * screen, ScreenRect screen_rect, ImageRect source_rect, Color color_filter, int spriteId = 0);

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