#pragma once
#include "bitmaps.h"
#include "entity.h"
#include "color_palette.h"

namespace crust
{
namespace aspects
{
    using namespace components;
    using PaletteRenderable = ecs::aspect<ColorPalette, IndexedBitmap>;

}

}
