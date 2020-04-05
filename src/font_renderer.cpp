#include "font_renderer.h"
#include "game_system.h"

fonts::BrodminGlyphInfo::BrodminGlyphInfo() : GlyphInfoTable(FontFace::Brodmin)
{
    Position pos = {0, 0};
    /* Setup number glyphs */
    for (char digit = '0'; digit <= '9'; ++digit)
    {
        glyphs[digit] = ImageRect{{pos}, {char_width, char_height}};
        pos.x += char_width;
    }
    /* Capital letters follow */
    pos.x = 0;
    pos.y += char_height;
    for (char digit = 'A'; digit <= 'Z'; ++digit)
    {
        glyphs[digit] = ImageRect{{pos}, {char_width, char_height}};
        pos.x += char_width;
        if (pos.x >= image_width)
        {
            pos.x = 0;
            pos.y += char_height;
        }
    }
    /* Don't care about the rest for now */
}

void BitmapFont::Render(Screen * surface, ScreenRect screen_rect, std::string_view text, Color color)
{
    for (char ch : text)
    {
        auto & glyph_rect = this->glyph_lookup.GetSourceRect(ch);
        this->font_bitmap.Draw(surface, screen_rect.pos, glyph_rect, color);
        screen_rect.pos.x += glyph_rect.size.x;
    }
}

FontRenderer::FontRenderer(BmpDecoder * bmp_decoder)
    : font_brodmin(bmp_decoder->LoadGrayscaleFromRGBA("resources/fonts/broddmin_5x10.bmp"), fonts::BrodminGlyphInfo{})
{

}

void FontRenderer::Render(FontFace font, Screen * screen, ScreenRect screen_rect, std::string_view text, Color color)
{
    /* There's not other font now*/
    assert(font == FontFace::Brodmin);
    this->font_brodmin.Render(screen, screen_rect, text, color);
}
