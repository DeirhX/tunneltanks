#include "base.h"
#include "level_pixel_surface.h"
#include <gamelib.h>
#include <level.h>
#include <random.h>
#include <screen.h>
#include <tank.h>
#include <tweak.h>
#include <types.h>

#include "game_system.h"

/* The constructor sets the video mode: */
Screen::Screen(bool is_fullscreen, Size render_surface_size) : is_fullscreen(is_fullscreen)
{
    this->Resize(render_surface_size);
}

/* Fills a surface with a blue/black pattern: */
void Screen::FillBackground()
{
    Size dim = GetSystem()->GetRenderer()->GetSurfaceResolution();

    GetSystem()->GetSurface()->DrawRectangle({{0, 0}, dim}, Palette.Get(Colors::Background));
    NativeScreenPosition o;
    for (o.y = 0; o.y < dim.y; o.y++)
    {
        for (o.x = (o.y % 2) * 2; o.x < dim.x; o.x += 4)
        {
            GetSystem()->GetSurface()->DrawRectangle(NativeRect{o, Size{1, 1}}, Palette.Get(Colors::BackgroundDot));
        }
    }
}

struct SinglePlayerLayout : public widgets::SharedLayout
{
    /* Player world view */
    constexpr static Offset view_offset = Offset{2, 2};
    constexpr static ScreenRect player_view_rect =
        ScreenRect{ScreenPosition{view_offset + padding},
                   Size{tweak::screen::RenderSurfaceSize.x - 2 * (view_offset.x + padding.x),
                        tweak::screen::RenderSurfaceSize.y - 2 * (view_offset.y + padding.y) - status_padding_top -
                            status_height}};

    /* Health + Energy bar */
    constexpr static ScreenRect tank_health_bars_rect =
        ScreenRect{ScreenPosition{9 + padding.x, tweak::screen::RenderSurfaceSize.y - 2 - status_height - padding.y},
                   Size{tweak::screen::RenderSurfaceSize.x - 16 - 2 * padding.x, status_height}};
    constexpr static ScreenRect energy_letter_rect = {ScreenPosition{3, tank_health_bars_rect.Top()}, Size{4, 5}};
    constexpr static ScreenRect health_letter_rect = {ScreenPosition{3, tank_health_bars_rect.Top() + 6}, Size{4, 5}};

    /* Lives remaining view */
    constexpr static ScreenRect lives_left_rect =
        ScreenRect{tank_health_bars_rect.Right() + 2, tank_health_bars_rect.Top() + 1, 2, status_height};
};

struct TwoPlayerLayout : public SinglePlayerLayout
{
    /* Player world view */
    constexpr static Offset view_offset = Offset{2, 2};
    constexpr static ScreenRect player_view_one = {
        ScreenPosition{player_view_rect.pos}, Size{player_view_rect.size - Size{player_view_rect.size.x / 2 + 1, 0}}};
    constexpr static ScreenRect player_view_two = {ScreenPosition{player_view_one.Right() + 2, player_view_rect.pos.y},
                                                   Size{player_view_one.size}};
    /* Health + Energy bar */
    constexpr static ScreenRect health_energy_one = {ScreenPosition{view_offset.x, tank_health_bars_rect.pos.y},
                                                     Size{player_view_one.size.x - energy_letter_rect.size.x / 2 -
                                                              lives_left_rect.size.x - 2 * lives_left_padding - 1,
                                                          tank_health_bars_rect.size.y}};
    constexpr static ScreenRect health_energy_two = {
        ScreenPosition{health_energy_one.Right() + energy_letter_rect.size.x + lives_left_rect.size.x * 2 + 3 +
                           2 * lives_left_padding + 3,
                       health_energy_one.pos.y},
        Size{health_energy_one.size}};
    constexpr static ScreenRect energy_letter_rect = {
        ScreenPosition{tweak::screen::RenderSurfaceSize.x / 2 - 2,
                       tweak::screen::RenderSurfaceSize.y - 2 - status_height},
        Size{4, 5}};
    constexpr static ScreenRect health_letter_rect = {ScreenPosition{energy_letter_rect.pos + Offset{0, 6}},
                                                      Size{4, 5}};

    /* Lives remaining view */
    constexpr static ScreenRect lives_left_rect_one =
        ScreenRect{health_energy_one.Right() + 2, health_energy_one.Top() + 1, 2, status_height};
    constexpr static ScreenRect lives_left_rect_two =
        ScreenRect{health_energy_two.Left() - health_letter_rect.size.x, health_energy_two.Top() + 1, 2, status_height};
};

void Screens::SinglePlayerScreenSetup(Screen * screen, World * world, Tank * player)
{
    /* Tank view and status below it*/
    auto window = std::make_unique<widgets::TankView>(SinglePlayerLayout::player_view_rect, player);
    auto crosshair = std::make_unique<widgets::Crosshair>(Position{0, 0}, screen, window.get());
    player->SetCrosshair(crosshair.get());

    screen->AddWidget(std::move(window));
    screen->AddWidget(std::move(crosshair));
    screen->AddStatus(SinglePlayerLayout::tank_health_bars_rect, player, true);
    screen->AddWidget(
        std::make_unique<widgets::LivesLeft>(SinglePlayerLayout::lives_left_rect, Orientation::Vertical, player));

    /* Add the letters E and H bitmaps: */
    screen->AddBitmap(SinglePlayerLayout::energy_letter_rect, &bitmaps::GuiEnergy,
                      static_cast<Color>(Palette.Get(Colors::StatusEnergy)));
    screen->AddBitmap(SinglePlayerLayout::health_letter_rect, &bitmaps::GuiHealth,
                      static_cast<Color>(Palette.Get(Colors::StatusHealth)));

    GetSystem()->GetCursor()->Show();
}

void Screens::TwoPlayerScreenSetup(Screen * screen, World * world, Tank * player_one, Tank * player_two)
{
    auto window = std::make_unique<widgets::TankView>(TwoPlayerLayout::player_view_one, player_one);
    auto crosshair =
        std::make_unique<widgets::Crosshair>(TwoPlayerLayout::player_view_one.Center(), screen, window.get());
    player_one->SetCrosshair(crosshair.get());

    screen->AddWidget(std::move(window));
    screen->AddWidget(std::move(crosshair));

    screen->AddStatus(TwoPlayerLayout::health_energy_one, player_one, false);
    screen->AddWidget(
        std::make_unique<widgets::LivesLeft>(TwoPlayerLayout::lives_left_rect_one, Orientation::Vertical, player_one));

    window = std::make_unique<widgets::TankView>(TwoPlayerLayout::player_view_two, player_two);
    crosshair = std::make_unique<widgets::Crosshair>(TwoPlayerLayout::player_view_two.Center(), screen, window.get());
    player_two->SetCrosshair(crosshair.get());

    screen->AddWidget(std::move(window));
    screen->AddWidget(std::move(crosshair));

    screen->AddStatus(TwoPlayerLayout::health_energy_two, player_two, true);
    screen->AddWidget(
        std::make_unique<widgets::LivesLeft>(TwoPlayerLayout::lives_left_rect_two, Orientation::Vertical, player_two));

    /* Add the letters E and H bitmaps: */
    screen->AddBitmap(TwoPlayerLayout::energy_letter_rect, &bitmaps::GuiEnergy,
                      static_cast<Color>(Palette.Get(Colors::StatusEnergy)));
    screen->AddBitmap(TwoPlayerLayout::health_letter_rect, &bitmaps::GuiHealth,
                      static_cast<Color>(Palette.Get(Colors::StatusHealth)));

    GetSystem()->GetCursor()->Hide();
}

void Screen::DrawPixel(ScreenPosition pos, Color color)
{
    //if (color.a == 0)
    //    return;
    GetSystem()->GetSurface()->DrawPixel(NativeScreenPosition{pos.x, pos.y}, color);
    return;

    //Offset adjusted_size = {/* Make some pixels uniformly larger to fill in given space relatively evenly  */
    //                        (pos.x * this->pixels_skip.x) / tweak::screen::RenderSurfaceSize.x,
    //                        (pos.y * this->pixels_skip.y) / tweak::screen::RenderSurfaceSize.y};
    //Offset adjusted_next = {((pos.x + 1) * this->pixels_skip.x) / tweak::screen::RenderSurfaceSize.x,
    //                        ((pos.y + 1) * this->pixels_skip.y) / tweak::screen::RenderSurfaceSize.y};

    ///* Final pixel position, adjusted by required scaling and offset */
    //auto native_pos = NativeScreenPosition{(pos.x * this->pixel_size.x) + this->screen_offset.x + adjusted_size.x,
    //                                       (pos.y * this->pixel_size.y) + this->screen_offset.y + adjusted_size.y};

    //auto final_size = Size{/* Compute size based on needing uneven scaling or not */
    //                       this->pixel_size.x + (adjusted_size.x != adjusted_next.x),
    //                       this->pixel_size.y + (adjusted_size.y != adjusted_next.y)};

    //GetSystem()->GetSurface()->DrawRectangle(NativeRect{native_pos, final_size}, color);
}

ScreenPosition Screen::FromNativeScreen(NativeScreenPosition native_pos)
{
    auto pos = ScreenPosition{native_pos.x, native_pos.y};
    pos.x -= this->screen_offset.x;
    pos.x -= pos.x / (int)this->pixel_size.x * (int)this->pixels_skip.x / tweak::screen::RenderSurfaceSize.x;
    pos.x /= (int)this->pixel_size.x;

    pos.y -= this->screen_offset.y;
    pos.y -= pos.y / (int)this->pixel_size.y * (int)this->pixels_skip.y / tweak::screen::RenderSurfaceSize.y;
    pos.y /= (int)this->pixel_size.y;

    return pos;
}

//NativeScreenPosition Screen::ToNativeScreen(ScreenPosition pos)
//{
//
//}

void Screen::DrawLevel()
{
    /* Erase everything */
    GetSystem()->GetSurface()->DrawRectangle(NativeRect{{0, 0}, GetSystem()->GetRenderer()->GetSurfaceResolution()},
                                             Palette.Get(Colors::Blank));
    /* Draw everything */
    std::for_each(this->widgets.begin(), this->widgets.end(), [this](auto & item) { item->Draw(this); });
}

void Screen::DrawCurrentMode()
{
    static std::chrono::microseconds time_elapsed = {};
    static int number_called = 0;
    Stopwatch<> elapsed;

    if (this->mode == SCREEN_DRAW_LEVEL)
    {
        this->DrawLevel();
    }

    /* Performance info */
    ++number_called;
    time_elapsed += elapsed.GetElapsed();
    if (number_called % 100 == 1)
    {
        auto average = time_elapsed / number_called;
        DebugTrace<4>("Screen::DrawCurrentMode takes on average %lld.%03lld ms\r\n", average.count() / 1000,
                      average.count() % 1000);
    }
    /* End performance info */
}

/* TODO: Change the screen API to better match gamelib... */

void Screen::SetFullscreen(bool new_fullscreen)
{

    if (this->is_fullscreen == new_fullscreen)
        return;

    this->is_fullscreen = new_fullscreen;

    /* Resize the screen to include the new fullscreen mode: */
    if (!is_fullscreen)
        this->Resize(Size{tweak::screen::WindowSize.x, tweak::screen::WindowSize.y});
    else
        this->Resize(this->screen_size);
}

/* Returns if successful */
void Screen::Resize(Size size)
{
    Size render_size;
    this->pixels_skip = {};
    this->screen_offset = {};
    this->pixel_size = {};

    /* Make sure that we aren't scaling to something too small: */
    size.x = std::max(tweak::screen::RenderSurfaceSize.x, size.x);
    size.y = std::max(tweak::screen::RenderSurfaceSize.y, size.y);

    /* A little extra logic for fullscreen: */
    size = GetSystem()->GetRenderer()->GetSurfaceResolution();

    auto current_fullscreen = GetSystem()->GetWindow()->IsFullscreen();
    if (this->is_fullscreen != current_fullscreen)
        GetSystem()->GetWindow()->Resize(size, this->is_fullscreen);

    /* What is the limiting factor in our scaling to maintain aspect ratio? */
    int yw = size.y * tweak::screen::RenderSurfaceSize.x;
    int xh = size.x * tweak::screen::RenderSurfaceSize.y;
    if (yw < xh)
    {
        /* size.y is. Correct aspect ratio using offset */
        render_size.x = (tweak::screen::RenderSurfaceSize.x * size.y) / (tweak::screen::RenderSurfaceSize.y);
        render_size.y = size.y;
        this->screen_offset.x = (size.x - render_size.x) / 2;
        this->screen_offset.y = 0;
    }
    else
    {
        /* size.x is. Correct aspect ratio using offset */
        render_size.x = size.x;
        render_size.y = (tweak::screen::RenderSurfaceSize.y * size.x) / (tweak::screen::RenderSurfaceSize.x);
        this->screen_offset.x = 0;
        this->screen_offset.y = (size.y - render_size.y) / 2;
    }

    /* Calculate the pixel sizing variables: */
    this->pixel_size.x = render_size.x / tweak::screen::RenderSurfaceSize.x;
    this->pixel_size.y = render_size.y / tweak::screen::RenderSurfaceSize.y;
    this->pixels_skip.x = render_size.x % tweak::screen::RenderSurfaceSize.x;
    this->pixels_skip.y = render_size.y % tweak::screen::RenderSurfaceSize.y;

    /* Draw a nice bg: */
    Screen::FillBackground();

    this->screen_size = size;

    /* Redraw the game: */
    this->DrawCurrentMode();
}

/* Set the current drawing mode: */
void Screen::SetLevelDrawMode(LevelPixelSurface * b)
{
    this->mode = SCREEN_DRAW_LEVEL;
    this->drawBuffer = b;
}

/*
void Screen::set_mode_menu( Menu *m) ;
void Screen::set_mode_map( Map *m) ;
*/

void Screen::AddWidget(std::unique_ptr<widgets::GuiWidget> && widget)
{
    if (this->mode != SCREEN_DRAW_LEVEL)
        return;
    // widgets::GuiWidget* raw_ptr = widget.get();
    this->widgets.emplace_back(std::move(widget));
    // return raw_ptr;
}

/* Window creation should only happen in Level-drawing mode: */
void Screen::AddWindow(Rect rect, Tank * task)
{
    if (this->mode != SCREEN_DRAW_LEVEL)
        return;
    this->widgets.emplace_back(std::make_unique<widgets::TankView>(rect, task));
}

/* We can add the health/energy status bars here: */
void Screen::AddStatus(Rect rect, Tank * tank, bool decreases_to_left)
{
    /* Verify that we're in the right mode, and that we have room: */
    if (this->mode != SCREEN_DRAW_LEVEL)
        return;

    /* Make sure that this status bar isn't too small: */
    if (rect.size.x <= 2 || rect.size.y <= 4)
        return;
    this->widgets.emplace_back(std::make_unique<widgets::StatusBar>(rect, tank, decreases_to_left));
}

/* We tell the graphics system about GUI graphics here:
 * 'color' has to be an ADDRESS of a color, so it can monitor changes to the
 * value, especially if the bit depth is changed...
 * TODO: That really isn't needed anymore, since we haven't cached mapped RGB
 *       values since the switch to gamelib... */
void Screen::AddBitmap(Rect rect, MonoBitmap * new_bitmap, Color color)
{
    /* Bitmaps are only for game mode: */
    if (this->mode != SCREEN_DRAW_LEVEL)
        return;
    if (!new_bitmap)
        return;
    this->widgets.emplace_back(std::make_unique<widgets::BitmapRender>(rect, new_bitmap, color));
}

void Screen::ClearGuiElements() { this->widgets.clear(); }

/* We don't check to see if gamelib needs the gui controller thing in this file.
 * That is handled in game.c: */
void Screen::AddController(Rect r)
{
    if (this->mode != SCREEN_DRAW_LEVEL)
        return;
    this->controller.r = r;
}
