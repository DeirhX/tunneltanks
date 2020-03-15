#pragma once
#include <level.h>
#include <types.h>
#include <tank.h>
#include <drawbuffer.h>

#include "bitmaps.h"
#include "gui_widgets.h"


class World;

typedef enum ScreenDrawMode {
	SCREEN_DRAW_INVALID,
	SCREEN_DRAW_LEVEL
} ScreenDrawMode;


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

	std::vector<std::unique_ptr<widgets::GuiWidget>> widgets;
	/*std::vector<std::unique_ptr<widgets::StatusBar>> statuses;
	std::vector<std::unique_ptr<widgets::BitmapRender>> bitmaps;*/

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
	void AddWidget(std::unique_ptr<widgets::GuiWidget>&& widget);
	void AddWindow(Rect rect, class Tank* task);
	void AddStatus(Rect r, class Tank* t, bool decreases_to_left);
	void AddBitmap(Rect r, Bitmap* bitmap, Color color);
	/* Call to remove all windows, statuses and bitmaps */
	void ClearGuiElements();
	
	void AddController(Rect r);

	static void FillBackground();
};

class Screens
{
public:
	static void SinglePlayerScreenSetup(Screen* screen, World* world, Tank* player);
	static void TwoPlayerScreenSetup(Screen* screen, World* world, Tank* player_one, Tank* player_two);
};
