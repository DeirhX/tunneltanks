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
    bool is_blocking_collidable = true;
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
    [[nodiscard]] virtual bool TestCollide(Position position) const;

    [[nodiscard]] bool IsBlockingCollision() const { return this->is_blocking_collidable; }
    [[nodiscard]] MachineConstructState GetState() const { return this->construct_state; }
    Reactor & GetReactor() { return this->reactor; }

    void SetState(MachineConstructState new_state);
    void SetPosition(Position new_position) { this->position = new_position; }
};

class MachineTemplate : public Machine
{
    Position origin_position;
  public:
    MachineTemplate(Position position, BoundingBox bounding_box);
    void Advance(Level *) override{}
    void ResetToOrigin();
    void Die(Level *) override{};
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

private:
    void Die(Level * level) override;
};

/* Visual template of Harvester without any game world interaction */
class HarvesterTemplate final : public MachineTemplate
{
  public:
    HarvesterTemplate(Position position);
    void Draw(Surface * surface) const override;
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

  private:
    void Die(Level * level) override;
};

/* Visual template of Charger without any game world interaction */
class ChargerTemplate final : public MachineTemplate
{
public:
    ChargerTemplate(Position position);
    void Draw(Surface * surface) const override;
};
