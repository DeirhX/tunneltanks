#pragma once
#include "item_list_adaptor.h"
#include "sprite.h"


/*
 * List of sprites to render in the world.
 */
class SpriteList : public ItemListAdaptor<Sprite, FailedInteraction>
{
  public:
    void Advance(class Terrain * level);
    void Draw(class Surface & surface);
};
