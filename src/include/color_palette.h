#pragma once
#include "types.h"
#include "color.h"

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
    ConcreteLow,
    ConcreteHigh,
    DirtContainerShot,
    ResourceInfoBackground,
    ResourceInfoOutline,
    RadarOutline,
    HarvesterInside,
    HarvesterOutline,
    ChargerOutline,
    EnergyFieldLow,
    EnergyFieldMedium,
    EnergyFieldHigh,
    EnergyShieldActive,
    EnergyShieldPassive,
    DirtShieldActive,
    DirtShieldPassive,
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
    //Color GetNoAlpha(Colors colorName);
    Color GetPrimary(TankColor index);
    Color *GetTank(TankColor index);

  private:
    void Set(Colors colorName, Color color);
    void SetPrimary(TankColor index, Color color);
    void SetTank(TankColor index, Color color_1, Color color_2, Color color_3);
};

/* Get your colors here! */
extern ColorPalette Palette;