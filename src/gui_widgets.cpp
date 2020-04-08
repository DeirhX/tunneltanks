#include "gui_widgets.h"

#include <string>

#include "base.h"
#include "game_system.h"
#include "screen.h"

#include "random.h"
#include "shape_renderer.h"
#include "tank.h"
#include "tweak.h"

namespace widgets
{

void widgets::TankView::DrawStatic(Screen *screen)
{
    int x, y;
    // int health = w->t->GetHealth();
    int energy = this->tank->GetEnergy();

    /* Don't do static if we have a lot of energy: */
    if (energy > STATIC_THRESHOLD)
    {
        this->counter = this->showing_static = 0;
        return;
    }

    if (!this->counter)
    {
        int intensity = 1000 * energy / STATIC_THRESHOLD;
        this->showing_static = !Random.Bool(intensity);
        this->counter =
            Random.Int(tweak::perf::TargetFps / 16, tweak::perf::TargetFps / 8) * this->showing_static ? 1u : 4u;
    }
    else
        this->counter--;

    if (!this->showing_static)
        return;

    auto black_bar_random_gen = [this]() {
        return Random.Int(1, this->screen_rect.size.x * this->screen_rect.size.y * STATIC_BLACK_BAR_SIZE / 1000);
    };

    /* Should we draw a black bar in the image? */
    int black_counter = Random.Bool(STATIC_BLACK_BAR_ODDS) ? black_bar_random_gen() : 0;
    int drawing_black = black_counter && Random.Bool(STATIC_BLACK_BAR_ODDS);

    /* Develop a static thing image for the window: */
    for (y = 0; y < this->screen_rect.size.y; y++) {
        for (x = 0; x < this->screen_rect.size.x; x++)
        {
            Color color;

            if (!energy)
            {
                screen->DrawPixel({x + this->screen_rect.pos.x, y + this->screen_rect.pos.y},
                                  Palette.GetPrimary(TankColor(Random.Int(0, 7))));
                continue;
            }

            /* Handle all of the black bar logic: */
            if (black_counter)
            {
                black_counter--;
                if (!black_counter)
                {
                    black_counter = black_bar_random_gen();
                    drawing_black = !drawing_black;
                }
            }

            /* Make this semi-transparent: */
            if (Random.Bool(STATIC_TRANSPARENCY))
                continue;

            /* Finally, select a color (either black or random) and draw: */
            color = drawing_black ? Palette.Get(Colors::Blank) : Palette.GetPrimary(TankColor(Random.Int(0, 7)));
            screen->DrawPixel({x + this->screen_rect.pos.x, y + this->screen_rect.pos.y}, color);
        }
    }
}

/* Will draw a window using the level's drawbuffer: */
void TankView::Draw(Screen *screen)
{
    Position tank_pos = this->tank->GetPosition();

    for (int y = 0; y < this->screen_rect.size.y; y++) {
        for (int x = 0; x < this->screen_rect.size.x; x++)
        {
            int screen_x = x + this->screen_rect.pos.x, screen_y = y + this->screen_rect.pos.y;

            RenderedPixel color = screen->GetLevelSurfaces()->terrain_surface.GetPixel(
                Position{x + tank_pos.x - this->screen_rect.size.x / 2, y + tank_pos.y - this->screen_rect.size.y / 2});
            screen->DrawPixel({screen_x, screen_y}, color);
        }
    }

    ShapeRenderer::DrawCircle(screen->GetScreenSurface(), Position{this->screen_rect.Center()}, 30,
                              Palette.Get(Colors::ResourceInfoBackground),
                                 Palette.Get(Colors::ResourceInfoOutline)); 

    /* Possibly overlay with static */
    this->DrawStatic(screen);
}

Position TankView::TranslatePosition(ScreenPosition screen_pos) const
{
    assert(this->screen_rect.IsInside(screen_pos));
    Offset offset = screen_pos - this->screen_rect.pos - (this->screen_rect.size / 2);
    return this->tank->GetPosition() + offset;
}

ScreenPosition TankView::TranslatePosition(Position world_pos) const
{
    Offset tank_offset = world_pos - this->tank->GetPosition();
    return ScreenPosition{tank_offset + this->screen_rect.pos + this->screen_rect.size / 2};
}

            /* Will draw two bars indicating the charge/health of a tank: */
/* TODO: This currently draws every frame. Can we make a dirty flag, and only
 *       redraw when it's needed? Also, can we put some of these calculations in
 *       the StatusBar structure, so they don't have to be done every frame? */
void StatusBar::Draw(Screen *screen)
{
    /* At what y value does the median divider start: */
    int mid_y = (this->screen_rect.size.y - 1) / 2;

    /* How many pixels high is the median divider: */
    int mid_h = (this->screen_rect.size.y % 2) ? 1u : 2u;

    /* How many pixels are filled in? */
    int energy_filled = this->tank->GetEnergy();
    int health_filled = this->tank->GetHealth();
    int half_energy_pixel = tweak::tank::StartingFuel / ((this->screen_rect.size.x - SharedLayout::status_border * 2) * 2);

    energy_filled += half_energy_pixel;

    energy_filled *= (this->screen_rect.size.x - SharedLayout::status_border * 2);
    energy_filled /= tweak::tank::StartingFuel;
    health_filled *= (this->screen_rect.size.x - SharedLayout::status_border * 2);
    health_filled /= tweak::tank::StartingShield;

    /* If we are decreasing to the right, we need to invert those values: */
    if (!this->decreases_to_left)
    {
        energy_filled = this->screen_rect.size.x - SharedLayout::status_border - energy_filled;
        health_filled = this->screen_rect.size.x - SharedLayout::status_border - health_filled;

        /* Else, we still need to shift it to the right by SharedLayout::status_border: */
    }
    else
    {
        energy_filled += SharedLayout::status_border;
        health_filled += SharedLayout::status_border;
    }

    /* Ok, lets draw this thing: */
    for (int y = 0; y < this->screen_rect.size.y; y++) {
        for (int x = 0; x < this->screen_rect.size.x; x++) {
            Color c;

            /* We round the corners of the status box: */
            if ((x == 0 || x == this->screen_rect.size.x - 1) && (y == 0 || y == this->screen_rect.size.y - 1))
                continue;

            /* Outer border draws background: */
            else if (y < SharedLayout::status_border || y >= this->screen_rect.size.y - SharedLayout::status_border ||
                     x < SharedLayout::status_border || x >= this->screen_rect.size.x - SharedLayout::status_border)
                c = Palette.Get(Colors::StatusBackground);

            /* We round the corners here a little bit too: */
            else if ((x == SharedLayout::status_border || x == this->screen_rect.size.x - SharedLayout::status_border - 1) &&
                     (y == SharedLayout::status_border || y == this->screen_rect.size.y - SharedLayout::status_border - 1))
                c = Palette.Get(Colors::StatusBackground);

            /* Middle seperator draws as backround, as well: */
            else if (y >= mid_y && y < mid_y + mid_h)
                c = Palette.Get(Colors::StatusBackground);

            /* Ok, this must be one of the bars. */
            /* Is this the filled part of the energy bar? */
            else if (y < mid_y && ((this->decreases_to_left && x < energy_filled) ||
                                   (!this->decreases_to_left && x >= energy_filled)))
                c = Palette.Get(Colors::StatusEnergy);

            /* Is this the filled part of the health bar? */
            else if (y > mid_y && ((this->decreases_to_left && x < health_filled) ||
                                   (!this->decreases_to_left && x >= health_filled)))
                c = Palette.Get(Colors::StatusHealth);

            /* Else, this must be the empty part of a bar: */
            else
                c = Palette.Get(Colors::Blank);

            screen->DrawPixel({x + this->screen_rect.pos.x, y + this->screen_rect.pos.y}, c);
        }
    }
}

void BitmapRender::Draw(Screen *screen)
{
    this->data->Draw(screen, this->screen_rect.pos,
                     ImageRect{{0, 0}, {this->data->GetSize().x, this->data->GetSize().y}}, this->color);
}

void LivesLeft::Draw(Screen *screen)
{
    assert(direction == Orientation::Vertical); // Implemennt horizontal when we need it
    if (direction == Orientation::Vertical)
    {
        int y_pos = 0;
        for (int life = 0; y_pos + 2 <= this->screen_rect.size.y; ++life)
        {
            Color such_color = (life < tank->GetLives()) ? this->color : Palette.Get(Colors::Blank);
            this->data->Draw(screen, ScreenPosition{this->screen_rect.pos} + Offset{0, y_pos}, such_color);
            y_pos += 1 + this->data->GetSize().y;
        }
    }
}

void Crosshair::UpdateVisual()
{
    this->screen_rect = ScreenRect{this->center.x - this->data->GetSize().x / 2, this->center.y - this->data->GetSize().y / 2,
                      this->data->GetSize().x, this->data->GetSize().y};
}

void Crosshair::MoveRelative(Offset offset)
{
    this->center += offset;
    this->center = ScreenPosition{this->parent_view->GetRect().MakeInside(this->center)};
    this->UpdateVisual();
}

void Crosshair::SetRelativePosition(const Tank * tank, DirectionF direction)
{
    SetWorldPosition(tank->GetPosition() + Offset{tweak::control::GamePadCrosshairRadius * direction});
    is_hidden = (direction == DirectionF{});
}

void Crosshair::SetScreenPosition(NativeScreenPosition position)
{
    this->center = screen->FromNativeScreen(position);
    this->center = ScreenPosition{this->parent_view->GetRect().MakeInside(this->center)};
    this->UpdateVisual();
    is_hidden = false;
}

void Crosshair::SetWorldPosition(Position position)
{
    this->center = parent_view->TranslatePosition(position);
    this->UpdateVisual();
    is_hidden = false;
}

void Crosshair::Draw(Screen *)
{
    if (!is_hidden)
        Parent::Draw(this->screen);
}

void ResourcesMinedDisplay::Draw(Screen * screen)
{
    /* Draw outline and background */
    ShapeRenderer::DrawRectangle(screen->GetScreenSurface(), Rect{this->screen_rect}, true,
                                 Palette.Get(Colors::ResourceInfoBackground),
                                 Palette.Get(Colors::ResourceInfoOutline)); 

    ScreenRect text_rect = {this->screen_rect.Left() + 2, this->screen_rect.Top() + 2, this->screen_rect.size.x - 4,
                            this->screen_rect.size.y - 4};
    GetSystem()->GetFontRenderer()->Render(FontFace::Brodmin, screen, text_rect, 
                                           std::to_string(this->tank->GetDirtMined() / 10),
                                           Palette.Get(Colors::StatusEnergy), HorizontalAlign::Right);
    text_rect.pos.y += 9;
    text_rect.size.y -= 9;
    GetSystem()->GetFontRenderer()->Render(FontFace::Brodmin, screen, text_rect,
                                           std::to_string(this->tank->GetRockMined() / 10),
                                           Palette.Get(Colors::StatusHealth), HorizontalAlign::Right);
}

} // namespace widgets
