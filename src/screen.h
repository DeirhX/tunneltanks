#pragma once

#include "bitmaps.h"
#include "color.h"
#include "gui_widgets.h"
#include <Terrain.h>
#include <tank.h>
#include <types.h>

class World;

enum class ScreenDrawMode
{
    Invalid,
    DrawLevel
};

struct GUIController
{
    Rect r;
};

class Screen
{
    bool is_fullscreen;

    Size screen_size = {};
    Offset screen_offset = {};

    std::vector<std::unique_ptr<widgets::GuiWidget>> widgets;

    GUIController controller;
    ScreenDrawMode mode = ScreenDrawMode::DrawLevel;

    LevelSurfaces * level_surfaces = nullptr;
    ScreenRenderSurface * screen_surface = nullptr;

  public:
    Screen(bool is_fullscreen, ScreenRenderSurface * render_surface);

    /* Resizing the screen: */
    bool GetFullscreen() const { return is_fullscreen; }
    void SetFullscreen(bool new_fullscreen);
    void Resize(Size size);

    /* Set the current drawing mode: */
    void SetDrawLevelSurfaces(LevelSurfaces * surfaces);

    LevelSurfaces * GetLevelSurfaces() { return this->level_surfaces; }
    ScreenRenderSurface * GetScreenSurface() { return this->screen_surface; }

    /* A few useful functions for external drawing: */
    void DrawPixel(ScreenPosition pos, Color color);

    /* These will say what virtual pixel a physical pixel resides on: */
    ScreenPosition FromNativeScreen(OffsetF offset) const;
    /*  Draw whatever it is now supposed to draw */
    void DrawCurrentMode();

    void DrawLevel();

  public:
    void AddWidget(std::unique_ptr<widgets::GuiWidget> && widget);
    void AddWindow(ScreenRect rect, class Tank & tank);
    void AddStatus(ScreenRect r, class Tank & tank, bool decreases_to_left);
    void AddBitmap(ScreenRect r, MonoBitmap & bitmap, Color color);
    /* Call to remove all windows, statuses and bitmaps */
    void ClearGuiElements();

    void AddController(Rect r);

    static void FillBackground();
};

class Screens
{
  public:
    static void SinglePlayerScreenSetup(Screen & screen, Tank & player);
    static void TwoPlayerScreenSetup(Screen & screen, Tank & player_one, Tank & player_two);
    static void AIViewSinglePlayerSetup(Screen & screen, Tank & player_one, Tank & player_two);
};
