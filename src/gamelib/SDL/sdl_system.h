#pragma once
#include "gamelib.h"
#include "sdl_renderer.h"

struct VideoConfig;

class SdlSystem : public GameSystem
{
    SdlWindow window;
    SdlRenderer renderer;
    SdlCursor cursor;
  public:
    explicit SdlSystem(VideoConfig video_config);
    SdlRenderer * GetRenderer() override { return &this->renderer; }
    SdlWindow * GetWindow() override { return &this->window; }
    SdlCursor * GetCursor() override { return &this->cursor; }
};

