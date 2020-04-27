#include "color.h"
#include "screen.h"
#include "shape_renderer.h"

void ShapeRenderer::DrawLine(Surface & surface, Position from, Position to, Color color)
{
    Offset diff = to - from;
    if (diff.x == 0 || diff.y == 0)
    {
        /* Easy horizontal / vertical line */
        Offset step = {std::clamp(diff.x, -1, +1), std::clamp(diff.y, -1, +1)};
        do
        {
            surface.SetPixel(from, color);
            from += step;
        } while (from != to);
    }
    else
    {
        /* Not so easy free-form line */
        int steps = std::max(std::abs(diff.x), std::abs(diff.y));
        OffsetF one_step = OffsetF{diff} / static_cast<float>(steps);
        PositionF curr = PositionF{from};
        PositionF target = PositionF{to};
        
        int curr_step = 0;
        do
        {
            surface.SetPixel(curr.ToIntPosition(), color);
            curr += one_step;
        } while (curr_step++ < steps);
    }
}

void ShapeRenderer::DrawLinePart(Surface * surface, Position from, Position to, int skip_pixels, int draw_pixels,
    Color color)
{
    Offset diff = to - from;
    int pixels_drawn = 0;
    const int start_draw = std::max(0, skip_pixels);
    const int stop_draw = start_draw + draw_pixels;
    int steps = 1 + std::max(std::abs(diff.x), std::abs(diff.y));

    if (diff.x == 0 || diff.y == 0)
    {
        /* Easy horizontal / vertical line */
        Offset step = {std::clamp(diff.x, -1, +1), std::clamp(diff.y, -1, +1)};
        do
        {
            if (pixels_drawn >= start_draw && pixels_drawn < stop_draw)
                surface->SetPixel(from, color);
            from += step;
            ++pixels_drawn;
        } while (--steps && pixels_drawn < stop_draw);
    }
    else
    {
        /* Not so easy free-form line */
        OffsetF one_step = OffsetF{diff} / static_cast<float>(std::max(std::abs(diff.x), std::abs(diff.y)));
        PositionF curr = PositionF{from};
        PositionF target = PositionF{to};

        do
        {
            if (pixels_drawn >= start_draw && pixels_drawn < stop_draw)
                surface->SetPixel(curr.ToIntPosition(), color);
            curr += one_step;
            ++pixels_drawn;
        } while (--steps && pixels_drawn < stop_draw);
    }
}

void ShapeRenderer::FillRectangle(Surface * surface, Rect rect, Color color)
{
    Position pos;
    for (pos.x = rect.Left(); pos.x < rect.Right(); ++pos.x)
        for (pos.y = rect.Top(); pos.y < rect.Bottom(); ++pos.y)
            surface->SetPixel(pos, color);
}


void ShapeRenderer::DrawFilledRectangle(Surface * surface, Rect screen_rect, bool round_corner_pixels, Color fill_color,
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

void ShapeRenderer::DrawRectangle(Surface * surface, Rect screen_rect, bool round_corners, Color outline_color)
{
    const int drawn_pixels = 2 * screen_rect.size.x + 2 * screen_rect.size.y - 4;
    if (!round_corners)
    {
        DrawRectanglePart(surface, screen_rect, 0, drawn_pixels, outline_color);
    }
    else
    {
        /* Draw separate lines, stopping and skipping over corners */
        int start_pixel = 1;
        DrawRectanglePart(surface, screen_rect, start_pixel, start_pixel + screen_rect.size.x - 2, outline_color);
        start_pixel = screen_rect.size.x;
        DrawRectanglePart(surface, screen_rect, start_pixel, start_pixel + screen_rect.size.y - 2, outline_color);
        start_pixel += screen_rect.size.y - 1 ;
        DrawRectanglePart(surface, screen_rect, start_pixel, start_pixel + screen_rect.size.x - 2, outline_color);
        start_pixel += screen_rect.size.x - 1;
        DrawRectanglePart(surface, screen_rect, start_pixel, start_pixel + screen_rect.size.y - 2, outline_color);
    }
}

void ShapeRenderer::DrawRectanglePart(Surface * surface, Rect screen_rect, int skip_pixels, int draw_pixels,
                                      Color outline_color)
{
    int pixels_drawn = 0;
    //const int start_draw = std::max(0, skip_pixels);
    //const int stop_draw = start_draw + draw_pixels;

    /* Draw outline using 4 lines */
    DrawLinePart(surface, {screen_rect.Left(), screen_rect.Top()}, 
                 {screen_rect.Right(), screen_rect.Top()},
                 skip_pixels, draw_pixels, outline_color);
    pixels_drawn += screen_rect.size.x;
    DrawLinePart(surface, {screen_rect.Right(), screen_rect.Top() + 1}, 
                 {screen_rect.Right(), screen_rect.Bottom() - 1},       
                 skip_pixels - pixels_drawn, draw_pixels - pixels_drawn,
                 outline_color);
    pixels_drawn += screen_rect.size.y - 2;
    DrawLinePart(surface, {screen_rect.Right(), screen_rect.Bottom()},
                 {screen_rect.Left(), screen_rect.Bottom()},
                 skip_pixels - pixels_drawn, draw_pixels - pixels_drawn, outline_color);
    pixels_drawn += screen_rect.size.x;
    DrawLinePart(surface, {screen_rect.Left(), screen_rect.Bottom() - 1},
                 {screen_rect.Left(), screen_rect.Top() + 1}, skip_pixels - pixels_drawn,
                 draw_pixels - pixels_drawn,
                 outline_color);
}

void ShapeRenderer::DrawCircle(Surface * surface, Position center, int radius, Color,
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
