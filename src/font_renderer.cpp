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
    pos.x += 1 * char_width;
    glyphs[' '] = ImageRect{{pos}, {char_width, char_height}};
    /* Don't care about the rest for now */
}

void BitmapFont::Render(Screen * surface, ScreenRect screen_rect, std::string_view text, Color color,
                        HorizontalAlign alignment)
{
    if (alignment == HorizontalAlign::Left)
    {   /* Easy, just let the bitmap renderer do the clipping into screen_rect */
        for (char ch : text)
        {
            auto & glyph_rect = this->glyph_lookup.GetSourceRect(ch);
            this->font_bitmap.Draw(surface, screen_rect, glyph_rect, color);
            screen_rect.pos.x += glyph_rect.size.x;
            screen_rect.size.x -= glyph_rect.size.x;
        }
    }
    else /* Alignment::Right */
    {    /* We'll need to do some clever clipping ourselves since normally it's clipped from the bottom right, we need to clip left */
        int horizontal_offset = 0;
        for (auto it = text.rbegin(); it != text.rend(); ++it)
        {
            ImageRect glyph_rect = this->glyph_lookup.GetSourceRect(*it);
            horizontal_offset += glyph_rect.size.x;
            ScreenRect target_rect;
            /* Decide if we have enough space to fit this letter*/
            if (horizontal_offset <= screen_rect.size.x)
            {   /* Enough space, render normally */
                target_rect = {{screen_rect.Right() - horizontal_offset + 1, screen_rect.Top()},
                               {glyph_rect.size.x, screen_rect.size.y}};
            }
            else
            {   /* Needs clipping, clip the source rect */
                const int remaining = std::max(0, screen_rect.size.x - horizontal_offset + glyph_rect.size.x);
                target_rect = {{screen_rect.Left(), screen_rect.Top()}, Size{remaining, screen_rect.size.y}};
                glyph_rect.pos.x += glyph_rect.size.x - remaining;
                glyph_rect.size.x = remaining;
            }
            this->font_bitmap.Draw(surface, target_rect, glyph_rect, color);
        }
    }
}

FontRenderer::FontRenderer(BmpDecoder * bmp_decoder)
    : font_brodmin(bmp_decoder->LoadGrayscaleFromRGBA("resources/fonts/broddmin_5x10.bmp"), fonts::BrodminGlyphInfo{})
{
}

void FontRenderer::Render(FontFace font, Screen * screen, ScreenRect screen_rect, std::string_view text, Color color,
                          HorizontalAlign alignment)
{
    /* There's not other font now*/
    assert(font == FontFace::Brodmin);
    this->font_brodmin.Render(screen, screen_rect, text, color, alignment);
}
