#pragma once

#include "link.h"
#include "tweak.h"
#include "types.h"

class Level;

enum class MachineConstructState
{
    Materializing,
    Transporting,
    Planted
};

class Machine : public Invalidable
{
  protected:
    MachineConstructState construct_state = MachineConstructState::Materializing;

    Position position;
    BoundingBox bounding_box;

    class Tank * owner;

    LinkPointSource link_source;
    Reactor reactor = tweak::rules::DefaultMachineReactor;

  protected:
    Machine(Position position, Tank * owner, Reactor reactor_, BoundingBox bounding_box);
  public:
    const Position & GetPosition() const { return this->position; }

    bool CheckAlive(Level * level);
    virtual void Die(Level * level) = 0;
    /* They will not be called via v-table, don't worry. Compile-time polymorphism only. Just so you don't   */
    virtual void Advance(Level * level) = 0;
    virtual void Draw(Surface * surface) const = 0;
    virtual bool IsColliding(Position position) const = 0;

    [[nodiscard]] MachineConstructState GetState() const { return this->construct_state; }
    Reactor & GetReactor() { return this->reactor; }

    void SetState(MachineConstructState new_state);
    void SetPosition(Position new_position) { this->position = new_position; }
};

enum class HarvesterType
{
    Dirt,
    Mineral,
};

class Harvester final : public Machine
{
    using Base = Machine;
    [[maybe_unused]] HarvesterType type;
    RepetitiveTimer harvest_timer{tweak::rules::HarvestTimer};

  public:
    static inline BoundingBox bounding_box = BoundingBox{Size{5, 5}};
  public:
    Harvester(Position position, HarvesterType type, Tank * tank)
        : Machine{
            position, tank,
            tweak::rules::HarvesterReactor,
            Harvester::bounding_box},
        type(type)
    {
    }
    void Advance(Level * level) override;
    void Draw(Surface * surface) const override;
    bool IsColliding(Position position) const override;

private:
    void Die(Level * level) override;
};

/* Visual template of Harvester without any game world interaction */
class HarvesterTemplate final : public Machine
{
  public:
    HarvesterTemplate(Position position)
        : Machine{position, nullptr, Reactor{ReactorCapacity{}}, Harvester::bounding_box}
    {
    }
    void Advance(Level * ) override {};
    void Draw(Surface * surface) const override;
    bool IsColliding(Position ) const override { return false; };
    void Die(Level * ) override {};
};


class Charger final : public Machine
{
    using Base = Machine;
    RepetitiveTimer charge_timer{tweak::rules::ChargeTimer};
  public:
    static inline BoundingBox bounding_box = BoundingBox{Size{5, 5}};

  public:
    Charger(Position position, Tank * tank) : Machine{position, tank, tweak::rules::ChargerReactor, Charger::bounding_box}
    {
    }

    void Advance(Level * level) override;
    void Draw(Surface * surface) const override;
    bool IsColliding(Position position) const override;

  private:
    void Die(Level * level) override;
};

/* Visual template of Charger without any game world interaction */
class ChargerTemplate final : public Machine
{
public:
    ChargerTemplate(Position position) : Machine{position, nullptr, Reactor{ReactorCapacity{}}, Charger::bounding_box}
    {
    }
    void Advance(Level *) override { };
    void Draw(Surface * surface) const override;
    bool IsColliding(Position) const override { return false; };
    void Die(Level * level) { };
};
