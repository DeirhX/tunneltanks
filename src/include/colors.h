#pragma once
#include "types.h"

enum class Colors
{
    First = 0,
    Blank = 0,
    Transparent,
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
    LifeDot = StatusHealth,
    DecalHigh,
    DecalLow,
    TankTurret,
    ConcreteShot,
    Size,
};

struct ColorPalette
{
  private:
    constexpr static int PrimaryColors = 8;

    Color32 Values[static_cast<int>(Colors::Size)];
    Color Primaries[PrimaryColors];
    Color Tanks[PrimaryColors][3];

  public:
    ColorPalette();

    Color32 Get(Colors colorName);
    //Color GetNoAlpha(Colors colorName);
    Color GetPrimary(TankColor index);
    Color *GetTank(TankColor index);

  private:
    void Set(Colors colorName, Color32 color);
    void SetPrimary(TankColor index, Color color);
    void SetTank(TankColor index, Color color_1, Color color_2, Color color_3);
};

/* Get your colors here! */
extern ColorPalette Palette;