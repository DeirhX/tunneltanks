#pragma once
#include "bitmaps.h"
#include <array>
#include <string_view>

class GameSystem;
class ScreenRenderSurface;
class BmpDecoder;


enum class FontFace
{
    Invalid,
    Brodmin,
};

struct GlyphInfoTable
{
    FontFace font_face;
    std::array<ImageRect, 256> glyphs = {};

    GlyphInfoTable(FontFace font_face) : font_face(font_face) {}

    const ImageRect & GetSourceRect(char glyph) const
    {
        assert(glyph >= 0 && static_cast<ssize_t>(glyph) < static_cast<ssize_t>(glyphs.size()));
        assert(glyphs[glyph] != ImageRect{});
        return glyphs[glyph];
    }
};

namespace fonts
{
    /* Glyph definitions of font Broddmin */
    struct BrodminGlyphInfo : public GlyphInfoTable
    {
        constexpr static int raw_width = 4;
        constexpr static int raw_height = 9;

        constexpr static int char_width = raw_width + 1;
        constexpr static int char_height = raw_height + 1;
        constexpr static int image_width = 50;
        constexpr static int image_height = 77;

        BrodminGlyphInfo();
    };

} // namespace fonts


/* BitmapFont:  Font with simple bits designating value/transparency
 *  Stored as uint_8 because operating with just bits is now going to be faster */
class BitmapFont
{
    MonoBitmap font_bitmap;
    GlyphInfoTable glyph_lookup;

  public:
    BitmapFont(MonoBitmap && bitmap, GlyphInfoTable && glyph_info) : font_bitmap(bitmap), glyph_lookup(glyph_info) { };
    void Render(Screen * screen, ScreenRect screen_rect, std::string_view text, Color color, HorizontalAlign alignment);
};

class FontRenderer
{
  private:
    BitmapFont font_brodmin;
  public:
    FontRenderer(BmpDecoder * game_system);

    void Render(FontFace font, Screen * screen, ScreenRect screen_rect, std::string_view text, Color color, 
                HorizontalAlign alignment = HorizontalAlign::Left);
};
