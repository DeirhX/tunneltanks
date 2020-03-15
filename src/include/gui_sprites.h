#pragma once
#include <cassert>
#include <cstddef>
#include <initializer_list>
#include <vector>


#include "types.h"

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

using Bitmap = ValueArray<char>;

namespace bitmaps
{
	inline auto GuiHealth = Bitmap(Size{ 4, 5 },
		{1,0,0,1,
		 1,0,0,1,
		 1,1,1,1,
		 1,0,0,1,
		 1,0,0,1 });

	inline auto GuiEnergy = Bitmap(Size{ 4, 5 },
		{1,1,1,1,
		 1,0,0,0,
		 1,1,1,0,
		 1,0,0,0,
		 1,1,1,1 });

	inline auto LifeDot = Bitmap(Size{ 2, 2 },
		{1,1,
		 1,1,});
}