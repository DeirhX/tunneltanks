#pragma once
#include <array>

#include "bitmaps.h"

namespace text
{

struct GlyphInfo
{
    uint8_t ansi_ordinal;
    NativeRect glyph_rect;
};
/* BitmapFont:  Font with simple bits designating value/transparency
 *  Stored as uint_8 because operating with just bits is now going to be faster */
class BitmapFont
{
    MonoBitmap font_bitmap;
    std::array<NativeRect, 256> glyph_lookup;
public:
    BitmapFont(MonoBitmap&& bitmap) : font_bitmap(bitmap) { };
};


class FontRenderer
{
  public:
    FontRenderer(std::string_view) {}
};

} // namespace text