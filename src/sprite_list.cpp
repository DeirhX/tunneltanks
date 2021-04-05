#include "sprite_list.h"

void SpriteList::Advance(Terrain * level)
{
    this->items.ForEach([level](Sprite & sprite) { sprite.Advance(level); });
}

void SpriteList::Draw(Surface & surface)
{
    this->items.ForEach([&surface](Sprite & sprite) { sprite.Draw(surface); });
}
