#pragma once
#include <types.h>

struct ControllerOutput
{
	Speed speed = { };
	bool is_shooting = false;
};

class Controller
{
public:
	virtual ~Controller() = default;
	virtual ControllerOutput ApplyControls(struct PublicTankInfo* tankPublic) = 0;

	virtual bool IsPlayer() = 0;
};
