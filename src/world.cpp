#include "world.h"


void World::Advance(class Level* level, class DrawBuffer* draw_buffer)
{
	/* Clear everything: */
	this->tank_list->for_each([=](Tank* t) {t->Clear(draw_buffer); });
	this->projectiles->Erase(draw_buffer, level);

	/* Charge a small bit of energy for life: */
	this->tank_list->for_each([=](Tank* t) {t->AlterEnergy(TANK_IDLE_COST); });

	/* See if we need to be healed: */
	this->tank_list->for_each([=](Tank* t) {t->TryBaseHeal(); });

	/* Move everything: */
	this->projectiles->Advance(level, this->tank_list.get());
	this->tank_list->for_each([=](Tank* t) {t->DoMove(this->tank_list.get()); });

	/* Draw everything: */
	this->projectiles->Draw(draw_buffer);
	this->tank_list->for_each([=](Tank* t) {t->Draw(draw_buffer); });
}
