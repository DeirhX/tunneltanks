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
    /* May hold private image data if not constructed from a global. If initialized, data_view always points to this. */
    std::shared_ptr<Container> data;
    /* Always points to the image data. May point into Container if data is unique to this instance. In order cases, may point to globally shared data. */
    std::span<DataType> data_view;

  public:
    /* In-place value initialization from hardcoded byte-array */
    ImageData(Size size, std::initializer_list<DataType> data) : size(size), data(std::make_shared<Container>(data))
    {
        assert(size.x * size.y == static_cast<int>(data.size()));
        data_view = std::span<DataType>(this->data->begin(), this->data->end());
    }
    ImageData(Size size, std::span<DataType> data) : size(size), data_view(data)
    {
        assert(size.x * size.y == static_cast<int>(data.size()));
    }
    /* Initialization for dynamic content */
    ImageData(Size size) : size(size)
    {
        data = std::make_shared<Container>();
        data->resize(size.x * size.y);
        data_view = std::span<DataType>(this->data->begin(), this->data->end());
    }
    /* Copy assignment and constructor - need to re-point data_view to data  */
    ImageData(const ImageData & other) noexcept { *this = other; }
    ImageData & operator=(const ImageData & other) noexcept
    {
        this->size = other.size;
        if (other.data)
        {
            this->data = other.data;
            this->data_view = std::span<DataType>(this->data->begin(), this->data->end());
        }
        else
            this->data_view = other.data_view;
        return *this;
    }
    /* Move assignment and constructor - need to re-point data_view to data  */
    ImageData(ImageData && other) noexcept { *this = std::move(other); }
    ImageData & operator=(ImageData && other) noexcept
    {
        this->size = other.size;
        if (other.data)
        {
            this->data = std::move(other.data);
            this->data_view = std::span<DataType>(this->data->begin(), this->data->end());
        }
        else
            this->data_view = other.data_view;
        return *this;
    }

    size_t GetLength() const { return data_view.size(); }
    Size GetSize() const { return this->size; }

    /* Read-write accessors */
    DataType & At(int index)
    {
        assert(index >= 0 && index < data_view.size());
        return *(data_view.begin() + index);
    }
    [[nodiscard]] const DataType & At(int index) const
    {
        assert(index >= 0 && index < data_view.size());
        return *(data_view.begin() + index);
    }
    DataType & operator[](int index) { return At(index); }
    [[nodiscard]] const DataType & operator[](int index) const { return At(index); }

    /* Iterator support */
    iterator begin() { return data_view.begin(); }
    iterator end() { return data_view.end(); }
    const_iterator cbegin() const { return data_view.cbegin(); }
    const_iterator cend() const { return data_view.cend(); }

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
        assert(size.Area() * spriteCount == static_cast<int>(data.size()));
    }
    /* In-place value initialization from byte-array span */
    SpriteImageData(Size size, std::span<DataType> data, int spriteCount = 1)
        : Base(Size(size.x, size.y * spriteCount), data), spriteCount(spriteCount), spriteSize(size)
    {
        assert(size.Area() * spriteCount == static_cast<int>(data.size()));
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
    using Base::Base;

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
 * Values contained may be looked up using a ColorPalette to assign colors
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