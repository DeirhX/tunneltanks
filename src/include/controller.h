#pragma once
#include <types.h>

struct ControllerOutput
{
	Speed speed = { };
	bool is_shooting_primary = false;
    bool is_shooting_secondary = false;
	DirectionF turret_dir = { 0, 0 };

    bool is_crosshair_absolute = false; /* Use either native screen position (mouse) or relative direction (gamepad) */
    NativeScreenPosition crosshair_screen_pos = {};
    DirectionF crosshair_direction = {};
};

class Controller
{
public:
	virtual ~Controller() = default;
	virtual ControllerOutput ApplyControls(struct PublicTankInfo* tankPublic) = 0;

	virtual bool IsPlayer() = 0;
};
