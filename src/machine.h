#pragma once

#include "link.h"
#include "tweak.h"
#include "types.h"

class Terrain;

enum class MachineConstructState
{
    Materializing,
    Transporting,
    Planted
};

/*
 * Machine: base class of all machines, placeable structures that can modify the environment, produce or transfer resources.
 *          usually owned by a player
 */
class Machine : public Invalidable
{
  protected:
    bool is_blocking_collidable = true;
    bool is_transported = false;
    bool is_template = false;
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

    bool CheckAlive(Terrain * level);
    virtual void Die(Terrain * level) = 0;
    /* They will not be called via v-table, don't worry. Compile-time polymorphism only. Just so you don't   */
    virtual void Advance(Terrain * level) = 0;
    virtual void Draw(Surface * surface) const = 0;

    [[nodiscard]] virtual bool TestCollide(Position position) const;

    [[nodiscard]] bool IsBlockingCollision() const { return this->GetState() == MachineConstructState::Planted; }
    [[nodiscard]] bool IsBeingTransported() const { return this->is_transported; }
    [[nodiscard]] MachineConstructState GetState() const { return this->construct_state; }
    Reactor & GetReactor() { return this->reactor; }

    void SetState(MachineConstructState new_state);
    void SetPosition(Position new_position);
    void SetIsTransported(bool new_value);
};


/*
 * Harvester: machine that harvests resources directly from environment and offers them to players 
 */

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
    void Advance(Terrain * level) override;
    void Draw(Surface * surface) const override;
  private:
    void Die(Terrain * level) override;
};

/*
 * Charger: power transmitter that is capable of linking into a power grid starting from base reactor
 *           and distributing this power to any other machine or player
 */
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

    void Advance(Terrain * level) override;
    void Draw(Surface * surface) const override;

  private:
    void Die(Terrain * level) override;
};

/*
 * MachineTemplate: template of a new machine that can construct it on BuildMachine() call. Usually part of base or
 *                   other place capable of building machines.
 */
class MachineTemplate : public Machine
{
    bool is_available = false;

    MaterialAmount build_cost = {};
    MaterialContainer * paying_container = nullptr;
    Position origin_position;
  protected:
    bool PayCost() const;
  public:
    MachineTemplate(Position position, BoundingBox bounding_box, MaterialAmount build_cost_, MaterialContainer & paying_host);

    void Advance(Terrain *) override;
    void ResetToOrigin();
    void Die(Terrain *) override {}

    virtual Machine * PayAndBuildMachine() const = 0;

    [[nodiscard]] bool IsAvailable() const { return this->is_available; }
};


/* HarvesterTemplate: blueprint of a Harvester machine */
class HarvesterTemplate final : public MachineTemplate
{
    HarvesterType type = HarvesterType::Dirt;

  public:
    HarvesterTemplate(Position position, MaterialContainer & paying_host);
    void Draw(Surface * surface) const override;
    Machine * PayAndBuildMachine() const override;
};


/* ChargerTemplate: blueprint of a Charger machine */
class ChargerTemplate final : public MachineTemplate
{
public:
    ChargerTemplate(Position position, MaterialContainer & paying_host);
    void Draw(Surface * surface) const override;
    Machine * PayAndBuildMachine() const override;
};
