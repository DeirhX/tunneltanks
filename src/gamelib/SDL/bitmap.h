#pragma once
#include <memory>
#include <SDL.h>
#include <string_view>
#include <types.h>
#include <vector>

#include "bitmaps.h"

class BmpFile
{
	std::unique_ptr<SDL_Surface, void(*)(SDL_Surface*)> native_surface;
public:
	static void SaveToFile(const ColorBitmap& data, std::string_view file_name);
	static ColorBitmap LoadFromFile(std::string_view file_name);
};

