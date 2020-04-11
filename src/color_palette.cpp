#include "color_palette.h"
#include "base.h"

#include <cassert>

#include "types.h"

ColorPalette Palette;
/* Various colors for use in the game: */
ColorPalette::ColorPalette()
{
    Set(Colors::Blank, Color(0x00, 0x00, 0x00));
    Set(Colors::Transparent, Color(0x00, 0x00, 0x00, 0x00));
    Set(Colors::DirtHigh, Color(0xc3, 0x79, 0x30));
    Set(Colors::DirtLow, Color(0xba, 0x59, 0x04));
    Set(Colors::DirtGrow, Color(0x6a, 0x29, 0x02));
    Set(Colors::Rock, Color(0x9a, 0x9a, 0x9a));
    Set(Colors::FireHot, Color(0xff, 0x34, 0x08));
    Set(Colors::FireCold, Color(0xba, 0x00, 0x00));
    Set(Colors::ConcreteShot, Color(0xba, 0xba, 0xcc));
    Set(Colors::ConcreteLow, Color(0xa0, 0xa0, 0xa5));
    Set(Colors::ConcreteHigh, Color(0x80, 0x80, 0x85));
    Set(Colors::Background, Color(0x00, 0x00, 0x00));
    Set(Colors::BackgroundDot, Color(0x20, 0x20, 0x20));
    Set(Colors::StatusBackground, Color(0x65, 0x65, 0x65));
    Set(Colors::StatusEnergy, Color(0xf5, 0xeb, 0x1a));
    Set(Colors::StatusHealth, Color(0x26, 0xf4, 0xf2));
    Set(Colors::DecalLow, Color(0x28, 0x28, 0x28));
    Set(Colors::DecalHigh, Color(0x48, 0x38, 0x2f));
    Set(Colors::TankTurret, Color(0xf3, 0xeb, 0x1c));
    Set(Colors::DirtContainerShot, Color(0xaa, 0x50, 0x03));
    Set(Colors::ResourceInfoBackground, Color(0x00, 0x00, 0x00, 0x80));
    Set(Colors::ResourceInfoOutline, Color(0xff, 0xff, 0xff, 0xa0));
    Set(Colors::RadarOutline, Color(0xff, 0xff, 0xff, 0x20));
    Set(Colors::HarvesterInside, Color(0x00, 0x88, 0x00, 0xff));
    Set(Colors::HarvesterOutline, Color(0x9f, 0xff, 0xff, 0xa0));
    Set(Colors::ChargerOutline, Color(0x4f, 0x4f, 0xff, 0xa0));

        SetPrimary(0, Color(0x00, 0x00, 0x00));
    SetPrimary(1, Color(0xff, 0x00, 0x00));
    SetPrimary(2, Color(0x00, 0xff, 0x00));
    SetPrimary(3, Color(0xff, 0xff, 0x00));
    SetPrimary(4, Color(0x00, 0x00, 0xff));
    SetPrimary(5, Color(0xff, 0x00, 0xff));
    SetPrimary(6, Color(0x00, 0xff, 0xff));
    SetPrimary(7, Color(0xff, 0xff, 0xff));

    /* Blue tank: */
    SetTank(0, Color(0x2c, 0x2c, 0xff), Color(0x00, 0x00, 0xb6), Color(0xf3, 0xeb, 0x1c));
    /* Green tank: */
    SetTank(1, Color(0x00, 0xff, 0x00), Color(0x00, 0xaa, 0x00), Color(0xf3, 0xeb, 0x1c));
    /* Red tank: */
    SetTank(2, Color(0xff, 0x00, 0x00), Color(0xaa, 0x00, 0x00), Color(0xf3, 0xeb, 0x1c));
    /* Pink tank: */
    SetTank(3, Color(0xff, 0x99, 0x99), Color(0xaa, 0x44, 0x44), Color(0xf3, 0xeb, 0x1c));
    /* Purple tank: */
    SetTank(4, Color(0xff, 0x00, 0xff), Color(0xaa, 0x00, 0xaa), Color(0xf3, 0xeb, 0x1c));
    /* White tank: */
    SetTank(5, Color(0xee, 0xee, 0xee), Color(0x99, 0x99, 0x99), Color(0xf3, 0xeb, 0x1c));
    /* Aqua tank: */
    SetTank(6, Color(0x00, 0xff, 0xff), Color(0x00, 0xaa, 0xaa), Color(0xf3, 0xeb, 0x1c));
    /* Gray tank: */
    SetTank(7, Color(0x66, 0x66, 0x66), Color(0x33, 0x33, 0x33), Color(0xf3, 0xeb, 0x1c));
}

Color ColorPalette::Get(Colors colorName)
{
    assert(colorName >= Colors::First && colorName < Colors::Size);
    return Values[static_cast<int>(colorName)];
}

Color ColorPalette::GetPrimary(TankColor index)
{
    assert(index >= 0 && index < PrimaryColors);
    return Primaries[index];
}

Color * ColorPalette::GetTank(TankColor index)
{
    assert(index >= 0 && index < PrimaryColors);
    return Tanks[index];
}

void ColorPalette::Set(Colors colorName, Color color) { Values[static_cast<int>(colorName)] = color; }

void ColorPalette::SetPrimary(TankColor index, Color color)
{
    assert(index >= 0 && index < PrimaryColors);
    Primaries[index] = color;
}

void ColorPalette::SetTank(TankColor index, Color color_1, Color color_2, Color color_3)
{
    assert(index >= 0 && index < PrimaryColors);
    Tanks[index][0] = color_1;
    Tanks[index][1] = color_2;
    Tanks[index][2] = color_3;
}
