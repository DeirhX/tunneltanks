#pragma once
#include "bitmaps.h"
#include "colors.h"

class Screen;
class Tank;

namespace widgets
{
struct SharedLayout
{
    constexpr static Size padding = Size{0, 0};
    constexpr static int status_border = 1;
    constexpr static int status_padding_top = 2;
    constexpr static int lives_left_padding = 1;
    constexpr static int status_height = 11;
};

class GuiWidget
{
  public:
    virtual ~GuiWidget() = default;
    virtual void Draw(class Screen * screen) = 0;
};

/* Will draw a window using the level's drawbuffer: */
class TankView : public GuiWidget
{
    Rect rect;
    Tank * tank;

    int counter = 0;
    int showing_static = 0;

  public:
    TankView(Rect rect, class Tank * tank) : rect(rect), tank(tank) {}
    void Draw(Screen * screen) override;
    Position TranslatePosition(ScreenPosition screen_position) const;
    ScreenPosition TranslatePosition(Position screen_position) const;
    Rect GetRect() const { return rect; }

  private:
    /* Will randomly draw static to a window, based on a tank's health.  */
    void DrawStatic(Screen * screen);
};

/* Will draw two bars indicating the charge/health of a tank: */
class StatusBar : public GuiWidget
{
    Rect rect;
    Tank * tank;
    bool decreases_to_left;

  public:
    StatusBar(Rect rect, class Tank * tank, bool decrease_to_left)
        : rect(rect), tank(tank), decreases_to_left(decrease_to_left)
    {
    }
    void Draw(Screen * screen) override;
};

/* Will draw an arbitrary, static bitmap to screen*/
struct BitmapRender : public GuiWidget
{
    Rect rect;
    MonoBitmap * data;
    Color32 color;

  public:
    BitmapRender(Rect rect, MonoBitmap * bitmap_data, Color32 color) : rect(rect), data(bitmap_data), color(color) {}
    void Draw(Screen * screen) override;
};

struct LivesLeft : public BitmapRender
{
    Orientation direction;
    Tank * tank;

  public:
    LivesLeft(Rect rect, Orientation direction, Tank * tank)
        : BitmapRender(rect, &bitmaps::LifeDot, Palette.Get(Colors::LifeDot)), direction(direction), tank(tank)
    {
        assert(direction == Orientation::Vertical && rect.size.x == this->data->size.x ||
               direction == Orientation::Horizontal && rect.size.y == this->data->size.y);
    }
    void Draw(Screen * screen) override;
};

class Crosshair : public BitmapRender
{
    using Parent = BitmapRender;

    ScreenPosition center = {};
    Screen * screen = nullptr;
    TankView * parent_view = nullptr;
    bool is_hidden = true;
  public:
    Crosshair(Position pos, Screen * screen, TankView * parent_view)
        : BitmapRender(Rect{pos.x - 1, pos.y - 1, 3, 3}, &bitmaps::Crosshair, Palette.Get(Colors::FireCold)),
          screen(screen), parent_view(parent_view)
    {
    }

    void UpdateVisual();
    void MoveRelative(Offset offset);
    void SetRelativePosition(const Tank * tank, DirectionF direction);
    void SetScreenPosition(NativeScreenPosition position);
    [[nodiscard]] ScreenPosition GetScreenPosition() const { return this->center; }
    Position GetWorldPosition() const { return parent_view->TranslatePosition(GetScreenPosition()); }
    void SetWorldPosition(Position position);
    void Draw(Screen * screen) override;
};

} // namespace widgets