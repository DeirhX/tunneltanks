#pragma once
#include <cassert>
#include <cstddef>
#include <initializer_list>
#include <vector>


#include "color.h"
#include "types.h"

class Screen;

template <typename DataType = char> class ValueArray
{
  public:
    using Container = std::vector<DataType>;
    using iterator = typename Container::iterator;
    using const_iterator = typename Container::const_iterator;
    /* Wasteful to copy in dynamically-allocated memory. But expecting we'll be keeping bitmaps in files in near future
     * when it's gonna be needed. Hang on! */
  private:
    Container data;

  public:
    Size size;

    ValueArray(Size size, std::initializer_list<DataType> data) : data(data), size(size)
    {
        assert(size.x * size.y == int(data.size()));
    }
    ValueArray(Size size) : size(size) { data.resize(size.x * size.y); }

    size_t GetLength() const { return data.size(); }

    DataType &At(int index)
    {
        assert(index >= 0 && index < size.x * size.y);
        return data[index];
    }
    DataType &operator[](int index) { return At(index); }
    [[nodiscard]] const DataType &At(int index) const
    {
        assert(index >= 0 && index < size.x * size.y);
        return data[index];
    }
    [[nodiscard]] const DataType &operator[](int index) const { return At(index); }

    iterator begin() { return data.begin(); }
    iterator end() { return data.end(); }
    const_iterator cbegin() const { return data.cbegin(); }
    const_iterator cend() const { return data.cend(); }

    operator Container() const { return data; }
};

template <typename DataType> class Bitmap : public ValueArray<DataType>
{
    using Base = ValueArray<DataType>;

  protected:
    /* Draw entire bitmap */
    template <typename GetColorFunc>
    void Draw(Screen *screen, Position position, GetColorFunc GetPixelColor); /* Color GetPixelColor(int index) */
    /* Draw portion of bitmap */
    template <typename GetColorFunc>
    void Draw(Screen *screen, Position screen_pos, Rect source_rect,
              GetColorFunc GetPixelColor); /* Color GetPixelColor(int index) */

    Bitmap(Size size, std::initializer_list<DataType> data) : Base(size, data) {}
    Bitmap(Size size) : Base(size) {}

  public:
    [[nodiscard]] int ToIndex(Position position) const { return position.x + position.y * this->size.x; }
};

class MonoBitmap : public Bitmap<char>
{
    using Base = Bitmap<char>;

  public:
    MonoBitmap(Size size, std::initializer_list<char> data) : Bitmap<char>(size, data) {}
    MonoBitmap(Size size) : Bitmap<char>(size) {}
    /* Draw entire bitmap */
    void Draw(Screen *screen, Position position, Color color);
    /* Draw portion of bitmap */
    void Draw(Screen *screen, Position screen_pos, Rect source_rect, Color color);

  private:
    // int ToIndex(Position position) const { return position.x + position.y * size.x; }
};

class ColorBitmap : public Bitmap<Color>
{
    using Base = Bitmap<Color>;

  public:
    ColorBitmap(Size size, std::initializer_list<Color> data) : Bitmap<Color>(size, data) {}
    ColorBitmap(Size size) : Bitmap<Color>(size) {}
    /* Draw entire bitmap */
    void Draw(Screen *screen, Position screen_pos);
    void Draw(Screen *screen, Position screen_pos, Color color_filter);
    /* Draw portion of bitmap */
    void Draw(Screen *screen, Position screen_pos, Rect source_rect);
    void Draw(Screen *screen, Position screen_pos, Rect source_rect, Color color_filter);

  private:
    // int ToIndex(Position position) const { return position.x + position.y * size.x; }
};

namespace bitmaps
{
	inline auto GuiHealth = MonoBitmap(Size{ 4, 5 },
		{1,0,0,1,
		 1,0,0,1,
		 1,1,1,1,
		 1,0,0,1,
		 1,0,0,1 });

	inline auto GuiEnergy = MonoBitmap(Size{ 4, 5 },
		{1,1,1,1,
		 1,0,0,0,
		 1,1,1,0,
		 1,0,0,0,
		 1,1,1,1 });

	inline auto LifeDot = MonoBitmap(Size{ 2, 2 },
		{1,1,
		 1,1,});

	inline auto Crosshair = MonoBitmap(Size{ 3, 3},
		{ 0,1,0,
		  1,1,1,
		  0,1,0 });
}
