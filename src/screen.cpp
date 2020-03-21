#include "base.h"
#include "colors.h"
#include <drawbuffer.h>
#include <gamelib.h>
#include <level.h>
#include <random.h>
#include <screen.h>
#include <tank.h>
#include <tweak.h>
#include <types.h>

/* The constructor sets the video mode: */
Screen::Screen(bool is_fullscreen) : is_fullscreen(is_fullscreen)
{
    this->Resize({tweak::screen::size.x, tweak::screen::size.y});
}

/* Fills a surface with a blue/black pattern: */
void Screen::FillBackground()
{
    Size dim = gamelib_get_resolution();

    gamelib_draw_box({{0, 0}, dim}, Palette.Get(Colors::Background));
    NativeScreenPosition o;
    for (o.y = 0; o.y < dim.y; o.y++)
    {
        for (o.x = (o.y % 2) * 2; o.x < dim.x; o.x += 4)
        {
            gamelib_draw_box(NativeRect{o, Size{1, 1}}, Palette.Get(Colors::BackgroundDot));
        }
    }
}

void Screens::SinglePlayerScreenSetup(Screen * screen, World * world, Tank * player)
{
    int gui_shift = 0;
    /* Tank view and status below it*/
    auto window = std::make_unique<widgets::TankView>(
        Rect{Position{2, 2}, Size{GAME_WIDTH - 4, GAME_HEIGHT - 6 - tweak::screen::status_height}}, player);
    auto crosshair = std::make_unique<widgets::Crosshair>(Position{0, 0}, screen, window.get());
    player->SetCrosshair(crosshair.get());

    screen->AddWidget(std::move(window));
    screen->AddWidget(std::move(crosshair));
    auto status_rect = Rect(9 + gui_shift, GAME_HEIGHT - 2 - tweak::screen::status_height, GAME_WIDTH - 16 - gui_shift,
                            tweak::screen::status_height);
    screen->AddStatus(status_rect, player, true);
    screen->AddWidget(std::make_unique<widgets::LivesLeft>(
        Rect{status_rect.Right() + 2, status_rect.Top() + 1, 2, tweak::screen::status_height}, Orientation::Vertical,
        player));

    /* Add the E/S bitmaps: */
    screen->AddBitmap(Rect(3 + gui_shift, GAME_HEIGHT - 2 - tweak::screen::status_height, 4, 5), 
                      &bitmaps::GuiEnergy, static_cast<Color>(Palette.Get(Colors::StatusEnergy)));
    screen->AddBitmap(Rect(3 + gui_shift, GAME_HEIGHT - 2 - tweak::screen::status_height + 6, 4, 5),
                      &bitmaps::GuiHealth, static_cast<Color>(Palette.Get(Colors::StatusHealth)));

    gamelib_disable_cursor();
}

void Screens::TwoPlayerScreenSetup(Screen * screen, World * world, Tank * player_one, Tank * player_two)
{
    screen->AddWindow(Rect(2, 2, GAME_WIDTH / 2 - 3, GAME_HEIGHT - 6 - tweak::screen::status_height), player_one);
    auto status_rect = Rect(3, GAME_HEIGHT - 2 - tweak::screen::status_height, GAME_WIDTH / 2 - 5 - 2 - 4,
                            tweak::screen::status_height);
    screen->AddStatus(status_rect, player_one, false);
    screen->AddWidget(std::make_unique<widgets::LivesLeft>(
        Rect{status_rect.Right() + 2, status_rect.Top() + 1, 2, tweak::screen::status_height}, Orientation::Vertical,
        player_one));

    screen->AddWindow(Rect(GAME_WIDTH / 2 + 1, 2, GAME_WIDTH / 2 - 3, GAME_HEIGHT - 6 - tweak::screen::status_height),
                      player_two);
    status_rect = Rect(GAME_WIDTH / 2 + 2 + 2, GAME_HEIGHT - 2 - tweak::screen::status_height,
                       GAME_WIDTH / 2 - 5 - 3 - 4, tweak::screen::status_height);
    screen->AddStatus(status_rect, player_two, true);
    screen->AddWidget(std::make_unique<widgets::LivesLeft>(
        Rect{status_rect.Right() + 2, status_rect.Top() + 1, 2, tweak::screen::status_height}, Orientation::Vertical,
        player_two));

    /* Add the GUI bitmaps: */
    screen->AddBitmap(Rect(GAME_WIDTH / 2 - 2, GAME_HEIGHT - 2 - tweak::screen::status_height, 4, 5),
                      &bitmaps::GuiEnergy, static_cast<Color>(Palette.Get(Colors::StatusEnergy)));
    screen->AddBitmap(Rect(GAME_WIDTH / 2 - 2, GAME_HEIGHT - 2 - tweak::screen::status_height + 6, 4, 5),
                      &bitmaps::GuiHealth, static_cast<Color>(Palette.Get(Colors::StatusHealth)));

    gamelib_enable_cursor();
}

void Screen::DrawPixel(ScreenPosition pos, Color32 color)
{
    if (color.a == 0)
        return;

    Offset adjusted_size = {/* Make some pixels uniformly larger to fill in given space relatively evenly  */
                            (pos.x * this->pixels_skip.x) / GAME_WIDTH, (pos.y * this->pixels_skip.y) / GAME_HEIGHT};
    Offset adjusted_next = {((pos.x + 1) * this->pixels_skip.x) / GAME_WIDTH,
                            ((pos.y + 1) * this->pixels_skip.y) / GAME_HEIGHT};

    /* Final pixel position, adjusted by required scaling and offset */
    auto native_pos = NativeScreenPosition{(pos.x * this->pixel_size.x) + this->screen_offset.x + adjusted_size.x,
                                           (pos.y * this->pixel_size.y) + this->screen_offset.y + adjusted_size.y};

    auto final_size = Size{/* Compute size based on needing uneven scaling or not */
                           this->pixel_size.x + (adjusted_size.x != adjusted_next.x),
                           this->pixel_size.y + (adjusted_size.y != adjusted_next.y)};

    gamelib_draw_box(NativeRect{native_pos, final_size}, color);
}

ScreenPosition Screen::FromNativeScreen(NativeScreenPosition native_pos)
{
    auto pos = ScreenPosition{native_pos.x, native_pos.y};
    pos.x -= this->screen_offset.x;
    pos.x -= pos.x / (int)this->pixel_size.x * (int)this->pixels_skip.x / GAME_WIDTH;
    pos.x /= (int)this->pixel_size.x;

    pos.y -= this->screen_offset.y;
    pos.y -= pos.y / (int)this->pixel_size.y * (int)this->pixels_skip.y / GAME_HEIGHT;
    pos.y /= (int)this->pixel_size.y;

    return pos;
}

void Screen::DrawLevel()
{
    /* Erase everything */
    gamelib_draw_box(NativeRect{{0, 0}, gamelib_get_resolution()}, Palette.Get(Colors::Blank));
    /* Draw everything */
    std::for_each(this->widgets.begin(), this->widgets.end(), [this](auto & item) { item->Draw(this); });
}

void Screen::DrawCurrentMode()
{
    if (this->mode == SCREEN_DRAW_LEVEL)
    {
        this->DrawLevel();
    }
    // throw GameException("Invalid mode to draw");
}

/* TODO: Change the screen API to better match gamelib... */

void Screen::SetFullscreen(bool new_fullscreen)
{

    if (this->is_fullscreen == new_fullscreen)
        return;

    this->is_fullscreen = new_fullscreen;

    /* Resize the screen to include the new fullscreen mode: */
    if (!is_fullscreen)
        this->Resize(Size{tweak::screen::size.x, tweak::screen::size.y});
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
    size.x = std::max(GAME_WIDTH, size.x);
    size.y = std::max(GAME_HEIGHT, size.y);

    /* A little extra logic for fullscreen: */
    if (this->is_fullscreen)
        gamelib_set_fullscreen();
    else
        gamelib_set_window(size);

    size = gamelib_get_resolution();

    this->is_fullscreen = gamelib_get_fullscreen();

    /* What is the limiting factor in our scaling to maintain aspect ratio? */
    int yw = size.y * GAME_WIDTH;
    int xh = size.x * GAME_HEIGHT;
    if (yw < xh)
    {
        /* size.y is. Correct aspect ratio using offset */
        render_size.x = (GAME_WIDTH * size.y) / (GAME_HEIGHT);
        render_size.y = size.y;
        this->screen_offset.x = (size.x - render_size.x) / 2;
        this->screen_offset.y = 0;
    }
    else
    {
        /* size.x is. Correct aspect ratio using offset */
        render_size.x = size.x;
        render_size.y = (GAME_HEIGHT * size.x) / (GAME_WIDTH);
        this->screen_offset.x = 0;
        this->screen_offset.y = (size.y - render_size.y) / 2;
    }

    /* Calculate the pixel sizing variables: */
    this->pixel_size.x = render_size.x / GAME_WIDTH;
    this->pixel_size.y = render_size.y / GAME_HEIGHT;
    this->pixels_skip.x = render_size.x % GAME_WIDTH;
    this->pixels_skip.y = render_size.y % GAME_HEIGHT;

    /* Draw a nice bg: */
    Screen::FillBackground();

    this->screen_size = size;

    /* Redraw the game: */
    this->DrawCurrentMode();
}

/* Set the current drawing mode: */
void Screen::SetLevelDrawMode(DrawBuffer * b)
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
