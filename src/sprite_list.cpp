#include "pch.h"
#include "sprite_list.h"
namespace crust
{

void SpriteList::Advance(Terrain * level)
{
    this->items.ForEach([level](Sprite & sprite) { sprite.Advance(level); });
}

void SpriteList::Draw(Surface & surface)
{
    this->items.ForEach([&surface](Sprite & sprite) { sprite.Draw(surface); });
}

} // namespace crust