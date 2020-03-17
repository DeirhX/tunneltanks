#pragma once
#include <cassert>
#include <cstddef>
#include <initializer_list>
#include <vector>

#include "types.h"

class Screen;

template <typename DataType = char>
class ValueArray
{
	using Container = std::vector<DataType>;
	/* Wasteful to copy in dynamically-allocated memory. But expecting we'll be keeping bitmaps in files in near future when it's gonna be needed. Hang on! */
	Container data;
public:
	Size size;
	typename Container::iterator begin() { return data.begin(); }
	typename Container::iterator end() { return data.end(); }
public:
	ValueArray(Size size, std::initializer_list<DataType> data) : data(data), size(size)
	{
		assert(size.x * size.y == int(data.size()));
	};
	DataType& At(int index)
	{
		assert(index >= 0 && index < size.x * size.y);
		return data[index];
	}
};

template <typename DataType>
class Bitmap : public ValueArray<DataType>
{
	using Base = ValueArray<DataType>;
protected:
	/* Draw entire bitmap */
	template <typename GetColorFunc>
	void Draw(Screen* screen, Position position, GetColorFunc GetPixelColor); /* Color GetPixelColor(int index) */
	/* Draw portion of bitmap */
	template <typename GetColorFunc>
	void Draw(Screen* screen, Position screen_pos, Rect source_rect, GetColorFunc GetPixelColor); /* Color GetPixelColor(int index) */

	Bitmap(Size size, std::initializer_list<DataType> data) : Base(size, data) { }
public:
	[[nodiscard]] int ToIndex(Position position) const { return position.x + position.y * this->size.x; }
};


class MonoBitmap : public Bitmap<char>
{
	using Base = Bitmap<char>;
public:
	MonoBitmap(Size size, std::initializer_list<char> data) : Bitmap<char>(size, data) { }
	/* Draw entire bitmap */
	void Draw(Screen* screen, Position position, Color color);
	/* Draw portion of bitmap */
	void Draw(Screen* screen, Position screen_pos, Rect source_rect, Color color);
private:
	//int ToIndex(Position position) const { return position.x + position.y * size.x; }
};

class ColorBitmap : public Bitmap<Color>
{
	using Base = Bitmap<Color>;
public:
	ColorBitmap(Size size, std::initializer_list<Color> data) : Bitmap<Color>(size, data) { }
	/* Draw entire bitmap */
	void Draw(Screen* screen, Position screen_pos);
	void Draw(Screen* screen, Position screen_pos, Color color_filter);
	/* Draw portion of bitmap */
	void Draw(Screen* screen, Position screen_pos, Rect source_rect);
	void Draw(Screen* screen, Position screen_pos, Rect source_rect, Color color_filter);
private:
	//int ToIndex(Position position) const { return position.x + position.y * size.x; }
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
}
