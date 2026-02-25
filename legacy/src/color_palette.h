#pragma once
#include "types.h"
#include "color.h"

#include <array>
#include <span>

namespace crust
{

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
    ChargerInside,
    ChargerOutline,
    EnergyFieldLow,
    EnergyFieldMedium,
    EnergyFieldHigh,
    EnergyShieldActive,
    EnergyShieldPassive,
    DirtShieldActive,
    DirtShieldPassive,
    MaterialStatusOutline,
    MaterialStatusFill,
    LinkActive,
    LinkTheoretical,
    LinkBlocked,
    FailedInteraction,
    Size,
};

struct ColorPalette
{
  private:
    constexpr static int PrimaryColors = 8;
    constexpr static int TankColors = 3;

    std::array<Color, static_cast<int>(Colors::Size)> Values;
    std::array<Color, PrimaryColors> Primaries;
    std::array<std::array<Color, TankColors>, PrimaryColors> Tanks;

  public:
    ColorPalette();

    Color Get(Colors colorName);
    //Color GetNoAlpha(Colors colorName);
    Color GetPrimary(TankColor index);
    std::span<Color, TankColors> GetTank(TankColor index);

    using ValuesLookup = std::span<Color, static_cast<size_t>(Colors::Size)>;
    using PrimariesLookup = std::span<Color, PrimaryColors>;
    using TankPrimariesLookup = std::span<Color, TankColors>;
    TankPrimariesLookup GetTankColorsLookup(TankColor color)
    {
        assert(color < Tanks.size());
        return TankPrimariesLookup(Tanks[color].begin(), Tanks[color].end());
    }
    PrimariesLookup GetPrimariesLookup() { return PrimariesLookup(Primaries.begin(), Primaries.end()); }
    ValuesLookup GetWorldLookup() { return ValuesLookup(Values.begin(), Values.end()); }

  private:
    void Set(Colors colorName, Color color);
    void SetPrimary(TankColor index, Color color);
    void SetTank(TankColor index, Color color_1, Color color_2, Color color_3);
};

/* Get your colors here! */
extern ColorPalette Palette;

namespace components
{
    class ColorLookup
    {
      public:
        enum class PaletteKind
        {
            World,
            Primaries,
            Tank,
        };

      private:
        PaletteKind kind;
        std::span<Color> lookup;

      public:
        ColorLookup(PaletteKind kind) : kind(kind)
        {
            switch (kind)
            {
            case PaletteKind::World:
                lookup = Palette.GetWorldLookup();
                break;
            case PaletteKind::Primaries:
                lookup = Palette.GetPrimariesLookup();
                break;
            default:
                assert(!"Unsupported PaletteKind");
            }
        }
        ColorLookup(PaletteKind kind, TankColor color) : kind(kind)
        {
            if (kind != PaletteKind::Tank)
                assert(!"Unsupported PaletteKind");
            else
                lookup = Palette.GetTankColorsLookup(color);
        }

        Color Lookup(int index) const
        {
            assert(lookup.size() > index && index >= 0);
            return lookup[index];
        }
        std::span<Color> Get() const { return lookup; }
    };
} // namespace components

} // namespace crust