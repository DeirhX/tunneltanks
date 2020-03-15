#pragma once
#include "colors.h"
#include "gui_sprites.h"
#include "tank.h"

class Screen;

namespace widgets
{
	class GuiWidget
	{
	public:
		virtual ~GuiWidget() = default;
		virtual void Draw(class Screen* screen) = 0;
	};

	/* Will draw a window using the level's drawbuffer: */
	class TankView : public GuiWidget
	{
		Rect  rect;
		Tank* tank;

		int counter = 0;
		int showing_static = 0;
	public:
		TankView(Rect rect, class Tank* tank) : rect(rect), tank(tank) {}
		void Draw(Screen* screen) override;
	private:
		/* Will randomly draw static to a window, based on a tank's health.  */
		void DrawStatic(Screen* screen);
	};

	/* Will draw two bars indicating the charge/health of a tank: */
	class StatusBar : public GuiWidget
	{
		Rect  rect;
		class Tank* tank;
		bool decreases_to_left;
	public:
		StatusBar(Rect rect, class Tank* tank, bool decrease_to_left) : rect(rect), tank(tank), decreases_to_left(decrease_to_left) {}
		void Draw(Screen* screen)override;
	};

	/* Will draw an arbitrary, static bitmap to screen*/
	struct BitmapRender : public GuiWidget
	{
		Rect  rect;
		Bitmap* data;
		Color color;
	public:
		BitmapRender(Rect rect, Bitmap* bitmap_data, Color color) : rect(rect), data(bitmap_data), color(color) {}
		void Draw(Screen* screen) override;
	};

	struct LivesLeft : public BitmapRender
	{
	public:
		LivesLeft(Rect rect) : BitmapRender(rect, &bitmaps::LifeDot, Palette.Get(Colors::LifeDot)) {}
		//void Draw(Screen* screen) override;
	};

}