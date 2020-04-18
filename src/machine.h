#pragma once

#include "render_surface.h"
#include "tweak.h"
#include "types.h"

class Level;

class Machine
{
  protected:
    Position position;
    BoundingBox bounding_box;

    class Tank * owner;

    class LinkPoint * link_point = nullptr;
    Reactor reactor = tweak::rules::DefaultMachineReactor;

    bool is_alive = true;

  private:
    /* Private use for move assignment  */
    Machine & operator=(const Machine & other) = default;
  protected:
    Machine(Position position, Tank * owner, Reactor reactor_, BoundingBox bounding_box);
    Machine(Machine && movable) noexcept;
    Machine & operator=(Machine && movable) noexcept;
    /* Thou shalt not copy these items, we own resources */
    Machine(const Machine & copyable) noexcept = delete;
    virtual ~Machine() { Invalidate(); }
  public:
    const Position & GetPosition() { return this->position; }

    bool IsInvalid() const { return !is_alive; }
    bool IsValid() const { return is_alive; }
    void Invalidate();

    bool CheckAlive(Level * level);
    virtual void Die(Level * level) = 0;
    /* They will not be called via v-table, don't worry. Compile-time polymorphism only. Just so you don't   */
    virtual void Advance(Level * level) = 0;
    virtual void Draw(Surface * surface) const = 0;

    Reactor & GetReactor() { return this->reactor; }
};

enum class HarvesterType
{
    Dirt,
    Mineral,
};

class Harvester final : public Machine
{
    using Base = Machine;
    HarvesterType type;

    RepetitiveTimer harvest_timer{tweak::rules::HarvestTimer};
  public:
    Harvester(Position position, HarvesterType type, Tank * tank)
        : Machine{
            position, tank,
            tweak::rules::HarvesterReactor,
            BoundingBox{Size{5, 5}}},
        type(type)
    {
    }

    void Advance(Level * level) override;
    void Draw(Surface * surface) const override;
    bool IsColliding(Position position) const;

private:
    void Die(Level * level) override;
};

class Charger final : public Machine
{
    using Base = Machine;
    RepetitiveTimer charge_timer{tweak::rules::ChargeTimer};

  public:
    Charger(Position position, Tank * tank)
        : Machine{position, tank, tweak::rules::ChargerReactor, BoundingBox{Size{5, 5}}}
    {
    }
    //Charger(Charger && movable) noexcept
    //{ }
    //Charger(const Charger & copyable) noexcept {}
    //Charger & operator=(const Charger & other) = default;
    void Advance(Level * level) override;
    void Draw(Surface * surface) const override;
    bool IsColliding(Position position) const;

  private:
    void Die(Level * level) override;
};
