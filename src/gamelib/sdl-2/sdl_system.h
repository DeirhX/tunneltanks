#pragma once
#include "bitmap.h"
#include "gamelib.h"
#include "sdl_renderer.h"

struct VideoConfig;

class SdlSystem : public GameSystem
{
    SdlWindow window;
    SdlRenderer renderer;
    SdlCursor cursor;
    SdlBmpDecoder bmp_decoder;
    FontRenderer font_renderer;
  public:
    explicit SdlSystem(VideoConfig video_config);
    SdlRenderer * GetRenderer() override { return &this->renderer; }
    SdlWindow * GetWindow() override { return &this->window; }
    SdlCursor * GetCursor() override { return &this->cursor; }
    SdlBmpDecoder * GetBmpDecoder() override { return &this->bmp_decoder; }
    FontRenderer * GetFontRenderer() override { return &this->font_renderer; }
};

