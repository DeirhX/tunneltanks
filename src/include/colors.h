#pragma once
#include "types.h"

enum class Colors
{
	First = 0,
	Blank = 0,
	DirtHigh,
	DirtLow,
	DirtGrow,
	Rock,
	FireHot,
	FireCold,
	Background,
	BackgroundDot,
	StatusBackground,
	StatusEnergy,
	StatusHealth,
	Decal,
	Size,
};


struct ColorPalette
{
private:
	constexpr static int PrimaryColors = 8;

	Color Values[static_cast<int>(Colors::Size)];
	Color Primaries[PrimaryColors];
	Color Tanks[PrimaryColors][3];
public:
	ColorPalette();

	Color Get(Colors colorName);
	Color GetPrimary(TankColor index);
	Color* GetTank(TankColor index);
private:
	void Set(Colors colorName, Color color);
	void SetPrimary(TankColor index, Color color);
	void SetTank(TankColor index, Color color_1, Color color_2, Color color_3);
};

/* Get your colors here! */
extern ColorPalette Palette;