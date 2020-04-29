#pragma once
#include <types.h>

struct ControllerOutput
{
	Speed speed = { };
    DirectionF turret_dir = {0, 0};

    /* Is shooting button pressed down */
	bool is_shooting_primary = false;
    bool is_shooting_secondary = false;
    bool is_shooting_tertiary = false;
    bool is_building_primary = false;
    /* Did the button became signaled this frame */
    bool switch_primary_weapon_next = false;
    bool switch_primary_weapon_prev = false;
    bool switch_secondary_weapon_next = false;
    bool switch_secondary_weapon_prev = false;
    bool build_primary = false;
    bool build_secondary = false;
    bool build_tertiary = false;

    /* Crosshair controls */
    bool is_crosshair_absolute = false; /* Use either native screen position (mouse) or relative direction (gamepad) */
    OffsetF crosshair_screen_pos = {}; /* Relative to native screen size - 0 = top/left, 1 = bottom/right */
    DirectionF crosshair_direction = {};
};

class Controller
{
public:
	virtual ~Controller() = default;
	virtual ControllerOutput ApplyControls(const struct PublicTankInfo & tankPublic) = 0;

	virtual bool IsPlayer() = 0;
};
