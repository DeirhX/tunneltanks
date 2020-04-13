#pragma once

#include "render_surface.h"
#include "tweak.h"
#include "types.h"

class Level;

class Machine
{
  protected:
    Position position;
    class Tank * owner;

    BoundingBox bounding_box;
    Health health;
    bool is_alive = true;

  protected:
    Machine(Position position, Tank * owner, Health health, BoundingBox bounding_box)
        : position(position), owner(owner), bounding_box(bounding_box), health(health)
    {
    }
    virtual ~Machine() { Invalidate(); }
  public:
    const Position & GetPosition() { return this->position; }

    bool IsInvalid() const { return !is_alive; }
    bool IsValid() const { return is_alive; }
    void Invalidate() { is_alive = false; }

    bool CheckAlive(Level * level);
    virtual void Die(Level * level) = 0;
    /* They will not be called via v-table, don't worry. Compile-time polymorphism only. Just so you don't   */
    virtual void Advance(Level * level) = 0;
    virtual void Draw(Surface * surface) const = 0;

    void AlterHealth(int shot_damage);
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
            Health{tweak::rules::HarvesterHP},
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
        : Machine{position, tank, Health{tweak::rules::ChargerHP}, BoundingBox{Size{5, 5}}}
    {
    }
    void Advance(Level * level) override;
    void Draw(Surface * surface) const override;
    bool IsColliding(Position position) const;

  private:
    void Die(Level * level) override;
};
