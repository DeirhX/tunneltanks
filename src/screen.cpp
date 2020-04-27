#include "level.h"
#include "screen.h"
#include "tank.h"
#include "tweak.h"
#include "types.h"
#include "world.h"
#include "game_system.h"

/* The constructor sets the video mode: */
Screen::Screen(bool is_fullscreen, ScreenRenderSurface * render_surface) : is_fullscreen(is_fullscreen), screen_surface(render_surface)
{
    this->Resize(screen_surface->GetSize());
}

/* Fills a surface with a blue/black pattern: */
void Screen::FillBackground()
{
    Size dim = GetSystem()->GetRenderer()->GetSurfaceResolution();

    GetSystem()->GetSurface()->FillRectangle({{0, 0}, dim}, Palette.Get(Colors::Background));
    ScreenPosition o;
    for (o.y = 0; o.y < dim.y; o.y++)
    {
        for (o.x = (o.y % 2) * 2; o.x < dim.x; o.x += 4)
        {
            GetSystem()->GetSurface()->FillRectangle(ScreenRect{o, Size{1, 1}}, Palette.Get(Colors::BackgroundDot));
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
        ScreenRect{tank_health_bars_rect.Right() + 3, tank_health_bars_rect.Top() + 1, 2, status_height};

    /* Resource overlays */
    constexpr static ScreenRect resource_overlay = ScreenRect{player_view_rect.pos, Size{20, 20}};
};

struct TwoPlayerLayout : public SinglePlayerLayout
{
    /* Player world view */
    constexpr static Offset view_offset = Offset{2, 2};
    constexpr static ScreenRect player_view_one = {
        ScreenPosition{player_view_rect.pos}, Size{player_view_rect.size - Size{player_view_rect.size.x / 2 + 1, 0}}};
    constexpr static ScreenRect player_view_two = {ScreenPosition{player_view_one.Right() + 3, player_view_rect.pos.y},
                                                   Size{player_view_one.size}};

    /* Health + Energy bars */
    constexpr static ScreenRect health_energy_one = {ScreenPosition{view_offset.x, tank_health_bars_rect.pos.y},
                                                     Size{player_view_one.size.x - energy_letter_rect.size.x / 2 -
                                                              lives_left_rect.size.x - 2 * lives_left_padding - 1,
                                                          tank_health_bars_rect.size.y}};
    constexpr static ScreenRect health_energy_two = {
        ScreenPosition{health_energy_one.Right() + 1 + energy_letter_rect.size.x + lives_left_rect.size.x * 2 + 3 +
                           2 * lives_left_padding + 3,
                       health_energy_one.pos.y},
        Size{health_energy_one.size}};

    /* Decorations of the health + energy bar */
    constexpr static ScreenRect energy_letter_rect = {
        ScreenPosition{tweak::screen::RenderSurfaceSize.x / 2 - 2,
                       tweak::screen::RenderSurfaceSize.y - 2 - status_height},
        Size{4, 5}};
    constexpr static ScreenRect health_letter_rect = {ScreenPosition{energy_letter_rect.pos + Offset{0, 6}},
                                                      Size{4, 5}};

    /* Lives remaining view */
    constexpr static ScreenRect lives_left_rect_one =
        ScreenRect{health_energy_one.Right() + 3, health_energy_one.Top() + 1, 2, status_height};
    constexpr static ScreenRect lives_left_rect_two =
        ScreenRect{health_energy_two.Left() - health_letter_rect.size.x, health_energy_two.Top() + 1, 2, status_height};

     /* Resource overlays */
    constexpr static ScreenRect resource_overlay_one = ScreenRect{player_view_one.pos, Size{20, 20}};
    constexpr static ScreenRect resource_overlay_two =
        ScreenRect{{player_view_two.Right() - 19, player_view_two.Top()}, Size{20, 20}};
};

struct AIViewOnePlayerLayout : public TwoPlayerLayout
{
    /* Player world view */
    constexpr static ScreenRect player_view_one = TwoPlayerLayout::player_view_one;
    constexpr static ScreenRect player_view_two = TwoPlayerLayout::player_view_two;

};

void Screens::SinglePlayerScreenSetup(Screen & screen, Tank & player)
{
    /* Tank view and status below it*/
    auto window = std::make_unique<widgets::TankView>(SinglePlayerLayout::player_view_rect, player);
    auto crosshair = std::make_unique<widgets::Crosshair>(ScreenPosition{0, 0}, screen, *window.get());
    player.SetCrosshair(crosshair.get());

    screen.AddWidget(std::move(window));
    screen.AddWidget(std::move(crosshair));
    screen.AddStatus(SinglePlayerLayout::tank_health_bars_rect, player, true);
    screen.AddWidget(
        std::make_unique<widgets::LivesLeft>(SinglePlayerLayout::lives_left_rect, Orientation::Vertical, player));

    /* Add the letters E and H bitmaps: */
    screen.AddBitmap(SinglePlayerLayout::energy_letter_rect, bitmaps::GuiEnergy,
                      static_cast<Color>(Palette.Get(Colors::StatusEnergy)));
    screen.AddBitmap(SinglePlayerLayout::health_letter_rect, bitmaps::GuiHealth,
                      static_cast<Color>(Palette.Get(Colors::StatusHealth)));

    /* Add resources owned overlay */
    screen.AddWidget(std::make_unique<widgets::ResourcesMinedDisplay>(SinglePlayerLayout::resource_overlay,
                                                                       HorizontalAlign::Left, player));

    GetSystem()->GetCursor()->Hide();
}

void Screens::TwoPlayerScreenSetup(Screen & screen, Tank & player_one, Tank & player_two)
{
    auto window = std::make_unique<widgets::TankView>(TwoPlayerLayout::player_view_one, player_one);
    auto crosshair =
        std::make_unique<widgets::Crosshair>(TwoPlayerLayout::player_view_one.Center(), screen, *window.get());
    player_one.SetCrosshair(crosshair.get());

    screen.AddWidget(std::move(window));
    screen.AddWidget(std::move(crosshair));

    screen.AddStatus(TwoPlayerLayout::health_energy_one, player_one, false);
    screen.AddWidget(
        std::make_unique<widgets::LivesLeft>(TwoPlayerLayout::lives_left_rect_one, Orientation::Vertical, player_one));

    window = std::make_unique<widgets::TankView>(TwoPlayerLayout::player_view_two, player_two);
    crosshair = std::make_unique<widgets::Crosshair>(TwoPlayerLayout::player_view_two.Center(), screen, *window.get());
    player_two.SetCrosshair(crosshair.get());

    screen.AddWidget(std::move(window));
    screen.AddWidget(std::move(crosshair));

    screen.AddStatus(TwoPlayerLayout::health_energy_two, player_two, true);
    screen.AddWidget(
        std::make_unique<widgets::LivesLeft>(TwoPlayerLayout::lives_left_rect_two, Orientation::Vertical, player_two));

    /* Add the letters E and H bitmaps: */
    screen.AddBitmap(TwoPlayerLayout::energy_letter_rect, bitmaps::GuiEnergy,
                      static_cast<Color>(Palette.Get(Colors::StatusEnergy)));
    screen.AddBitmap(TwoPlayerLayout::health_letter_rect, bitmaps::GuiHealth,
                      static_cast<Color>(Palette.Get(Colors::StatusHealth)));

    /* Add resources owned overlays */
    screen.AddWidget(std::make_unique<widgets::ResourcesMinedDisplay>(TwoPlayerLayout::resource_overlay_one,
                                                                       HorizontalAlign::Left, player_one));
    screen.AddWidget(std::make_unique<widgets::ResourcesMinedDisplay>(TwoPlayerLayout::resource_overlay_two,
                                                                       HorizontalAlign::Right, player_two));

    GetSystem()->GetCursor()->Show();
}

void Screens::AIViewSinglePlayerSetup(Screen & screen, Tank & player_one, Tank & player_two)
{
    Screens::TwoPlayerScreenSetup(screen, player_one, player_two);
}

void Screen::DrawPixel(ScreenPosition pos, Color color)
{
    //if (color.a == 0)
    //    return;
    GetSystem()->GetSurface()->SetPixel(ScreenPosition{pos.x, pos.y}, color);
    return;
}

ScreenPosition Screen::FromNativeScreen(OffsetF offset) const
{
    return {int(float(this->screen_size.x) * offset.x), int(float(this->screen_size.y) * offset.y)};
}

void Screen::DrawLevel()
{
    /* Erase everything */
    GetSystem()->GetSurface()->Clear();
    GetLevelSurfaces()->terrain_surface.OverlaySurface(&GetLevelSurfaces()->objects_surface);
    /* Draw everything */
    std::for_each(this->widgets.begin(), this->widgets.end(), [this](auto & item) { item->Draw(*this); });

    GetWorld()->GetLevel()->CommitPixels(GetLevelSurfaces()->objects_surface.GetChangeList());
    GetLevelSurfaces()->objects_surface.Clear();
}

void Screen::DrawCurrentMode()
{
    static std::chrono::microseconds time_elapsed = {};
    static int number_called = 0;
    Stopwatch<> elapsed;

    if (this->mode == ScreenDrawMode::DrawLevel)
    {
        this->DrawLevel();
    }

    /* Performance info */
    ++number_called;
    time_elapsed += elapsed.GetElapsed();
    if (number_called % 100 == 0)
    {
        auto average = time_elapsed / number_called;
        DebugTrace<4>("Screen::DrawCurrentMode takes on average %lld.%03lld ms\r\n", average.count() / 1000,
                      average.count() % 1000);
        time_elapsed = {};
        number_called = 0;
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
    auto current_fullscreen = GetSystem()->GetWindow()->IsFullscreen();
    if (this->is_fullscreen != current_fullscreen)
        GetSystem()->GetWindow()->Resize(size, this->is_fullscreen);

    /* Draw a nice bg: */
    Screen::FillBackground();
    this->screen_size = size;
}

void Screen::SetDrawLevelSurfaces(LevelSurfaces * surfaces)
{
    this->mode = ScreenDrawMode::DrawLevel;
    this->level_surfaces = surfaces;
}

void Screen::AddWidget(std::unique_ptr<widgets::GuiWidget> && widget)
{
    // widgets::GuiWidget* raw_ptr = widget.get();
    this->widgets.emplace_back(std::move(widget));
    // return raw_ptr;
}

/* Window creation should only happen in Level-drawing mode: */
void Screen::AddWindow(ScreenRect rect, Tank & tank)
{
    this->widgets.emplace_back(std::make_unique<widgets::TankView>(rect, tank));
}

/* We can add the health/energy status bars here: */
void Screen::AddStatus(ScreenRect rect, Tank & tank, bool decreases_to_left)
{
    /* Make sure that this status bar isn't too small: */
    if (rect.size.x <= 2 || rect.size.y <= 4)
        return;
    this->widgets.emplace_back(std::make_unique<widgets::StatusBar>(rect, tank, decreases_to_left));
}

void Screen::AddBitmap(ScreenRect rect, MonoBitmap & new_bitmap, Color color)
{
    this->widgets.emplace_back(std::make_unique<widgets::BitmapRender>(rect, new_bitmap, color));
}

void Screen::ClearGuiElements() { this->widgets.clear(); }

/* We don't check to see if gamelib needs the gui controller thing in this file.
 * That is handled in game.c: */
void Screen::AddController(Rect r)
{
    this->controller.r = r;
}
