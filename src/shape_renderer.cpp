#include "shape_renderer.h"
#include "color.h"
#include "screen.h"

void ShapeRenderer::FillRectangle(Surface * surface, Rect rect, Color color)
{
    Position pos;
    for (pos.x = rect.Left(); pos.x < rect.Right(); ++pos.x)
        for (pos.y = rect.Top(); pos.y < rect.Bottom(); ++pos.y)
            surface->SetPixel(pos, color);
}


void ShapeRenderer::DrawRectangle(Surface * surface, Rect screen_rect, bool round_corner_pixels, Color fill_color,
                                  Color outline_color)
{
    for (int x = screen_rect.Left(); x <= screen_rect.Right(); ++x)
        for (int y = screen_rect.Top(); y <= screen_rect.Bottom(); ++y)
        {
            if (round_corner_pixels && 
                ((x == screen_rect.Left() && y == screen_rect.Top()) ||
                 (x == screen_rect.Right() && y == screen_rect.Top()) ||
                 (x == screen_rect.Left() && y == screen_rect.Bottom()) ||
                 (x == screen_rect.Right() && y == screen_rect.Bottom())))
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
                surface->SetPixel(Position{x, y}, fill_color);
            }
            else
            {
                surface->SetPixel(Position{x, y}, outline_color);
            }
        }
}

void ShapeRenderer::DrawRectanglePart(Surface * surface, Rect screen_rect, int skip_pixels, int draw_pixels,
    Color outline_color)
{
    int pixels_drawn = 0;
    const int start_draw = skip_pixels;
    const int stop_draw = start_draw + draw_pixels;
    for (int x = screen_rect.Left(); x <= screen_rect.Right(); ++x)
        for (int y = screen_rect.Top(); y <= screen_rect.Bottom(); ++y)
        {
            /* Are we inside edges?  */
            if ((x == screen_rect.Left() || x == screen_rect.Right() || y == screen_rect.Top() ||
                y == screen_rect.Bottom()))
            {
                if (pixels_drawn >= skip_pixels && pixels_drawn < stop_draw)
                    surface->SetPixel(Position{x, y}, outline_color);
                ++pixels_drawn;
            }
            if (pixels_drawn >= stop_draw)
                return;
        }
}

void ShapeRenderer::DrawCircle(Surface * surface, Position center, int radius, Color fill_color,
                               Color outline_color)
{
    /* Start on right side of circle and mirror every drawn pixel into 8 octants */
    int offset_x = radius;
    int offset_y = 0;
    int radius_sqr = radius * radius;

    auto radius_error = [radius_sqr](int x, int y)
    {
        return std::abs(x * x + y * y - radius_sqr);
    };

    while (offset_y <= offset_x)
    {
        if (offset_y && offset_y != offset_x)
        {   /* First pass should draw only 4 beginning pixels on x, -x, y and -y axes. Rest diverge into 8 directions. */
            surface->SetPixel(center + Offset{-offset_x, -offset_y}, outline_color);
            surface->SetPixel(center + Offset{offset_x, offset_y}, outline_color);
            surface->SetPixel(center + Offset{-offset_y, offset_x}, outline_color);
            surface->SetPixel(center + Offset{offset_y, -offset_x}, outline_color);
        }
        surface->SetPixel(center + Offset{-offset_y, -offset_x}, outline_color);
        surface->SetPixel(center + Offset{offset_y, offset_x}, outline_color);
        surface->SetPixel(center + Offset{-offset_x, offset_y}, outline_color);
        surface->SetPixel(center + Offset{offset_x, -offset_y}, outline_color);

        /* Decide if we end up with distance error from center by going directly up 
         *  or by going up and left (this is then mirrored into every octant)
         */
        int up_error = radius_error(offset_x, offset_y);
        int left_error = radius_error(offset_x - 1, offset_y);
        if (left_error < up_error)
            --offset_x;
        /* Always advance up*/
        ++offset_y;
    }
}

Position ShapeInspector::GetRandomPointInCircle(Position center, int radius)
{
    DirectionF direction = math::Radians{Random.Float(0, math::two_pi)}.ToDirection();
    float distance = Random.Float(0, static_cast<float>(radius));
    return center + Offset(distance * direction);
}
