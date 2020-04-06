#include "shape_renderer.h"
#include "color.h"
#include "screen.h"

void ShapeRenderer::DrawRectangle(Screen * screen, ScreenRect screen_rect, bool round_corner_pixels, Color fill_color,
                                  Color outline_color)
{
    for (int x = screen_rect.Left(); x <= screen_rect.Right(); ++x)
        for (int y = screen_rect.Top(); y <= screen_rect.Bottom(); ++y)
        {
            if (round_corner_pixels && (x == screen_rect.Left() && y == screen_rect.Top()) ||
                (x == screen_rect.Right() && y == screen_rect.Top()) ||
                (x == screen_rect.Left() && y == screen_rect.Bottom()) ||
                (x == screen_rect.Right() && y == screen_rect.Bottom()))
            {
                /* Don't draw if rounded */
                continue;
            }

            /* Are we inside edges?  */
            if (x != screen_rect.Left() && x != screen_rect.Right() && y != screen_rect.Top() &&
                y != screen_rect.Bottom())
            {
                if (fill_color.a == 0)
                    continue;
                screen->DrawPixel(ScreenPosition{x, y}, fill_color);
            }
            else
            {
                screen->DrawPixel(ScreenPosition{x, y}, outline_color);
            }
        }
}
