#pragma once
#include "link.h"
#include "machine.h"
#include "resources.h"
#include "tweak.h"
#include "types.h"

class Surface;
class World;

/*
 * Tank Base, part of the level, capable of building machines 
 */
class TankBase
{
    /* All your base are belong...same size */
    static constexpr Size BaseSize = Size{tweak::base::BaseSize, tweak::base::BaseSize};

    Position position = {-1, -1};
    BoundingBox bounding_box = {BaseSize};
    /* Owner tank */
    TankColor color = {-1};
    /* Link to power grid */
    LinkPoint * link_point{};

    /* Held energy and resources */
    Reactor reactor = tweak::base::Reactor;
    MaterialContainer materials = tweak::base::MaterialContainer;

    /* Machine templates it offers */
    ChargerTemplate * base_charger_template = nullptr;
    HarvesterTemplate * base_harvester_template = nullptr;

  public:
    TankBase() = default;
    explicit TankBase(Position position, TankColor color);
    void BeginGame();

  private:
    /* Initialization */
    void RegisterLinkPoint(World * world);
    void CreateMachineTemplates(World * world);
    void DrawMaterialStorage(Surface * surface) const;
  public:
    [[nodiscard]] Position GetPosition() const { return this->position; }
    // [[nodiscard]] LinkPoint * GetLinkPoint() const { return this->link_point; }
    [[nodiscard]] TankColor GetColor() const { return this->color; }
    [[nodiscard]] const MaterialContainer & GetResources() const { return this->materials; }
    [[nodiscard]] bool IsInside(Position position) const;

    /* Resource manipulation, charging and recharging */
    void AbsorbResources(MaterialContainer & other);
    void AbsorbResources(MaterialContainer & other, MaterialAmount rate);
    void GiveResources(MaterialContainer & other, MaterialAmount rate);
    void GiveResources(Reactor & other, ReactorState rate);
    void RechargeTank(Tank * tank);

    void Draw(Surface * surface) const;
    void Advance();
};
