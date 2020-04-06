#pragma once
#include "bitmaps.h"
#include "color_palette.h"

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
  protected:
    ScreenRect screen_rect;
  public:
    GuiWidget(ScreenRect screen_rect) : screen_rect(screen_rect) {}
    virtual ~GuiWidget() = default;
    ScreenRect GetRect() const { return screen_rect; }

    virtual void Draw(class Screen * screen) = 0;
};

/* Will draw a window using the level's drawbuffer: */
class TankView : public GuiWidget
{
    Tank * tank;

    int counter = 0;
    int showing_static = 0;

  public:
    TankView(ScreenRect screen_rect, class Tank * tank) : GuiWidget(screen_rect), tank(tank) {}
    void Draw(Screen * screen) override;
    Position TranslatePosition(ScreenPosition screen_position) const;
    ScreenPosition TranslatePosition(Position screen_position) const;

  private:
    /* Will randomly draw static to a window, based on a tank's health.  */
    void DrawStatic(Screen * screen);
};

/* Will draw two bars indicating the charge/health of a tank: */
class StatusBar : public GuiWidget
{
    Tank * tank;
    bool decreases_to_left;

  public:
    StatusBar(ScreenRect screen_rect, class Tank * tank, bool decrease_to_left)
        : GuiWidget(screen_rect), tank(tank), decreases_to_left(decrease_to_left)
    {
    }
    void Draw(Screen * screen) override;
};

/* Will draw an arbitrary, static bitmap to screen*/
struct BitmapRender : public GuiWidget
{
    MonoBitmap * data;
    Color color;

  public:
    BitmapRender(ScreenRect screen_rect, MonoBitmap * bitmap_data, Color color) : GuiWidget(screen_rect), data(bitmap_data), color(color) {}
    void Draw(Screen * screen) override;
};

struct LivesLeft : public BitmapRender
{
    Orientation direction;
    Tank * tank;

  public:
    LivesLeft(ScreenRect rect, Orientation direction, Tank * tank)
        : BitmapRender(rect, &bitmaps::LifeDot, Palette.Get(Colors::LifeDot)), direction(direction), tank(tank)
    {
        assert(direction == Orientation::Vertical && rect.size.x == this->data->GetSize().x ||
               direction == Orientation::Horizontal && rect.size.y == this->data->GetSize().y);
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
    Crosshair(ScreenPosition pos, Screen * screen, TankView * parent_view)
        : BitmapRender(ScreenRect{pos.x - 1, pos.y - 1, 3, 3}, &bitmaps::Crosshair, Palette.Get(Colors::FireCold)),
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

class ResourcesMinedDisplay : public GuiWidget
{
public:
    ResourcesMinedDisplay(ScreenRect screen_rect) : GuiWidget(screen_rect) {}
    void Draw(Screen * screen) override;
};

} // namespace widgets