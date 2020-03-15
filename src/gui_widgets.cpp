#include "base.h"
#include "gui_widgets.h"
#include "screen.h"

#include "tank.h"
#include "random.h"
#include "tweak.h"

namespace widgets {

	void widgets::TankView::DrawStatic(Screen* screen) {
		int x, y;
		//int health = w->t->GetHealth();
		int energy = this->tank->GetEnergy();

		/* Don't do static if we have a lot of energy: */
		if (energy > STATIC_THRESHOLD) {
			this->counter = this->showing_static = 0;
			return;
		}

		if (!this->counter) {
			int intensity = 1000 * energy / STATIC_THRESHOLD;
			this->showing_static = !Random.Bool(intensity);
			this->counter = Random.Int(tweak::perf::TargetFps / 16, tweak::perf::TargetFps / 8) * this->showing_static ? 1u : 4u;

		}
		else this->counter--;

		if (!this->showing_static) return;

		auto black_bar_random_gen = [this]() { return Random.Int(1, this->rect.size.x * this->rect.size.y * STATIC_BLACK_BAR_SIZE / 1000); };

		/* Should we draw a black bar in the image? */
		int black_counter = Random.Bool(STATIC_BLACK_BAR_ODDS) ? black_bar_random_gen() : 0;
		int drawing_black = black_counter && Random.Bool(STATIC_BLACK_BAR_ODDS);

		/* Develop a static thing image for the window: */
		for (y = 0; y < this->rect.size.y; y++)
			for (x = 0; x < this->rect.size.x; x++) {
				Color color;

				if (!energy) {
					screen->DrawPixel({ x + this->rect.pos.x, y + this->rect.pos.y }, Palette.GetPrimary(TankColor(Random.Int(0, 7))));
					continue;
				}

				/* Handle all of the black bar logic: */
				if (black_counter) {
					black_counter--;
					if (!black_counter) {
						black_counter = black_bar_random_gen();
						drawing_black = !drawing_black;
					}
				}

				/* Make this semi-transparent: */
				if (Random.Bool(STATIC_TRANSPARENCY)) continue;

				/* Finally, select a color (either black or random) and draw: */
				color = drawing_black ? Palette.Get(Colors::Blank) : Palette.GetPrimary(TankColor(Random.Int(0, 7)));
				screen->DrawPixel({ x + this->rect.pos.x, y + this->rect.pos.y }, color);
			}
	}

	/* Will draw a window using the level's drawbuffer: */
	void TankView::Draw(Screen* screen) {

		Position pos = this->tank->GetPosition();

		for (int y = 0; y < this->rect.size.y; y++) {
			for (int x = 0; x < this->rect.size.x; x++) {
				int screen_x = x + this->rect.pos.x, screen_y = y + this->rect.pos.y;

				Color c = screen->GetDrawBuffer()->GetPixel(Position{ x + pos.x - this->rect.size.x / 2, y + pos.y - this->rect.size.y / 2 });
				screen->DrawPixel({ screen_x, screen_y }, c);
			}
		}

		/* Possibly overlay with static */
		this->DrawStatic(screen);
	}

	/* Will draw two bars indicating the charge/health of a tank: */
	/* TODO: This currently draws every frame. Can we make a dirty flag, and only
	 *       redraw when it's needed? Also, can we put some of these calculations in
	 *       the StatusBar structure, so they don't have to be done every frame? */
	void StatusBar::Draw(Screen* screen) {
		/* At what y value does the median divider start: */
		int mid_y = (this->rect.size.y - 1) / 2;

		/* How many pixels high is the median divider: */
		int mid_h = (this->rect.size.y % 2) ? 1u : 2u;

		/* How many pixels are filled in? */
		int energy_filled = this->tank->GetEnergy();
		int health_filled = this->tank->GetHealth();
		int half_energy_pixel = tweak::tank::StartingFuel / ((this->rect.size.x - tweak::screen::status_border * 2) * 2);

		energy_filled += half_energy_pixel;

		energy_filled *= (this->rect.size.x - tweak::screen::status_border * 2);
		energy_filled /= tweak::tank::StartingFuel;
		health_filled *= (this->rect.size.x - tweak::screen::status_border * 2);
		health_filled /= tweak::tank::StartingShield;

		/* If we are decreasing to the right, we need to invert those values: */
		if (!this->decreases_to_left) {
			energy_filled = this->rect.size.x - tweak::screen::status_border - energy_filled;
			health_filled = this->rect.size.x - tweak::screen::status_border - health_filled;

			/* Else, we still need to shift it to the right by tweak::screen::status_border: */
		}
		else {
			energy_filled += tweak::screen::status_border;
			health_filled += tweak::screen::status_border;
		}

		/* Ok, lets draw this thing: */
		for (int y = 0; y < this->rect.size.y; y++) {
			for (int x = 0; x < this->rect.size.x; x++) {
				Color c;

				/* We round the corners of the status box: */
				if ((x == 0 || x == this->rect.size.x - 1) && (y == 0 || y == this->rect.size.y - 1))
					continue;

				/* Outer border draws background: */
				else if (y < tweak::screen::status_border || y >= this->rect.size.y - tweak::screen::status_border ||
					x < tweak::screen::status_border || x >= this->rect.size.x - tweak::screen::status_border)
					c = Palette.Get(Colors::StatusBackground);

				/* We round the corners here a little bit too: */
				else if ((x == tweak::screen::status_border || x == this->rect.size.x - tweak::screen::status_border - 1) &&
					(y == tweak::screen::status_border || y == this->rect.size.y - tweak::screen::status_border - 1))
					c = Palette.Get(Colors::StatusBackground);

				/* Middle seperator draws as backround, as well: */
				else if (y >= mid_y && y < mid_y + mid_h)
					c = Palette.Get(Colors::StatusBackground);

				/* Ok, this must be one of the bars. */
				/* Is this the filled part of the energy bar? */
				else if (y < mid_y &&
					((this->decreases_to_left && x < energy_filled) ||
					(!this->decreases_to_left && x >= energy_filled)))
					c = Palette.Get(Colors::StatusEnergy);

				/* Is this the filled part of the health bar? */
				else if (y > mid_y &&
					((this->decreases_to_left && x < health_filled) ||
					(!this->decreases_to_left && x >= health_filled)))
					c = Palette.Get(Colors::StatusHealth);

				/* Else, this must be the empty part of a bar: */
				else
					c = Palette.Get(Colors::Blank);

				screen->DrawPixel({ x + this->rect.pos.x, y + this->rect.pos.y }, c);
			}
		}
	}

	void BitmapRender::Draw(Screen* screen)
	{
		this->data->Draw(screen, this->rect.pos, this->color);
	}

	void LivesLeft::Draw(Screen* screen)
	{
		assert(direction == Direction::Vertical); // Implemennt horizontal when we need it
		if (direction == Direction::Vertical) 
		{
			int y_pos = 0;
			for (int life = 0; life < tank->GetLives() && y_pos + 2 <= this->rect.size.y; ++life)
			{
				this->data->Draw(screen, Position{ this->rect.pos } + Offset{ 0, y_pos }, this->color);
				y_pos += 1 + this->data->size.y;
			}
		}
	}
}
