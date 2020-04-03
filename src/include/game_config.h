#pragma once
#include "levelgen.h"
#include "types.h"

struct VideoConfig
{
    Size resolution;
    bool is_fullscreen;
    Size render_surface_size;
};

struct GameConfig
{
    VideoConfig video_config;
    Size level_size;
    LevelGenerator level_generator;
    bool is_debug;
    int player_count;
    int rand_seed;
    bool use_ai = true;
};
