#pragma once
#include <level.h>
#include <types.h>
#include <tank.h>
#include <drawbuffer.h>

#include "guisprites.h"


typedef enum ScreenDrawMode {
	SCREEN_DRAW_INVALID,
	SCREEN_DRAW_LEVEL
} ScreenDrawMode;

class GuiWidget
{
public:
	virtual void Draw(class Screen* screen) = 0;
};

/* Will draw a window using the level's drawbuffer: */
class Window : public GuiWidget
{
	Rect  rect;
	class Tank* tank;
	
	int counter = 0;
	int showing_static = 0;
public:
	Window(Rect rect, class Tank* tank) : rect(rect), tank(tank) {}
	void Draw(class Screen* screen) override;
private:
	/* Will randomly draw static to a window, based on a tank's health.  */
	void DrawStatic(class Screen* screen);
};

/* Will draw two bars indicating the charge/health of a tank: */
class StatusBar : public GuiWidget
{
	Rect  rect;
	class Tank* tank;
	bool decreases_to_left;
public:
	StatusBar(Rect rect, class Tank* tank, bool decrease_to_left) : rect(rect), tank(tank), decreases_to_left(decrease_to_left) {}
	void Draw(class Screen* screen)override;
};

/* Will draw an arbitrary, static bitmap to screen*/
struct BitmapRender : public GuiWidget
{
	Rect  rect;
	Bitmap* data;
	Color color;
public:
	BitmapRender(Rect rect, Bitmap* bitmap_data, Color color) : rect(rect), data(bitmap_data), color(color) {}
	void Draw(class Screen* screen)override;
};

struct GUIController
{
	Rect r;
};


class Screen {

	bool is_fullscreen;

	Size screen_size = {};
	Offset screen_offset = {};
	Size pixel_size = {};
	Size pixels_skip = {};

	std::vector<Window> windows;
	std::vector<StatusBar> statuses;
	std::vector<BitmapRender> bitmaps;

	GUIController controller;
	ScreenDrawMode mode = SCREEN_DRAW_INVALID;
	DrawBuffer* drawBuffer = nullptr;

public:
	Screen(bool is_fullscreen);

	/* Resizing the screen: */
	bool GetFullscreen() { return is_fullscreen; }
	void SetFullscreen(bool is_fullscreen);
	void Resize(Size size);

	/* Set the current drawing mode: */
	void SetLevelDrawMode(DrawBuffer* b);
	/* Source contents of the screen */
	DrawBuffer* GetDrawBuffer() { return drawBuffer; }

	/* A few useful functions for external drawing: */
	void DrawPixel(ScreenPosition pos, Color color);
	/* These will say what virtual pixel a physical pixel resides on: */
	Position ScreenToWorld(ScreenPosition pos);
	/*  Draw whatever it is now supposed to draw */
	void DrawCurrentMode();

	void DrawLevel();
public:
	void AddWindow(Rect rect, class Tank* task);
	void AddStatus(Rect r, class Tank* t, bool decreases_to_left);
	void AddBitmap(Rect r, Bitmap* bitmap, Color color);
	/* Call to remove all windows, statuses and bitmaps */
	void ClearGuiElements();
	
	void AddController(Rect r);

	static void FillBackground();
};
