#pragma once
#include "bitmaps.h"
#include "colors.h"

class Screen;
class Tank;

namespace widgets
{
class GuiWidget
{
  public:
    virtual ~GuiWidget() = default;
    virtual void Draw(class Screen *screen) = 0;
};

/* Will draw a window using the level's drawbuffer: */
class TankView : public GuiWidget
{
    Rect rect;
    Tank *tank;

    int counter = 0;
    int showing_static = 0;

  public:
    TankView(Rect rect, class Tank *tank) : rect(rect), tank(tank) {}
    void Draw(Screen *screen) override;

  private:
    /* Will randomly draw static to a window, based on a tank's health.  */
    void DrawStatic(Screen *screen);
};

/* Will draw two bars indicating the charge/health of a tank: */
class StatusBar : public GuiWidget
{
    Rect rect;
    Tank *tank;
    bool decreases_to_left;

  public:
    StatusBar(Rect rect, class Tank *tank, bool decrease_to_left)
        : rect(rect), tank(tank), decreases_to_left(decrease_to_left)
    {
    }
    void Draw(Screen *screen) override;
};

/* Will draw an arbitrary, static bitmap to screen*/
struct BitmapRender : public GuiWidget
{
    Rect rect;
    MonoBitmap *data;
    Color color;

  public:
    BitmapRender(Rect rect, MonoBitmap *bitmap_data, Color color) : rect(rect), data(bitmap_data), color(color) {}
    void Draw(Screen *screen) override;
};

struct LivesLeft : public BitmapRender
{
    Orientation direction;
    Tank *tank;

  public:
    LivesLeft(Rect rect, Orientation direction, Tank *tank)
        : BitmapRender(rect, &bitmaps::LifeDot, Palette.Get(Colors::LifeDot)), direction(direction), tank(tank)
    {
        assert(direction == Orientation::Vertical && rect.size.x == this->data->size.x ||
               direction == Orientation::Horizontal && rect.size.y == this->data->size.y);
    }
    void Draw(Screen *screen) override;
};

class Crosshair : public BitmapRender
{
    ScreenPosition center = {};
    Screen *screen = nullptr;
    TankView *parent_view = nullptr;

  public:
    Crosshair(Position pos, Screen *screen, TankView *parent_view)
        : BitmapRender(Rect{pos.x - 1, pos.y - 1, 3, 3}, &bitmaps::Crosshair, Palette.Get(Colors::FireHot)),
          screen(screen), parent_view(parent_view)
    {
    }

    void SetCenter(NativeScreenPosition position);
    // void Draw(Screen* screen) override;
};

} // namespace widgets