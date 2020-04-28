#pragma once
#include "controller.h"
#include "link.h"
#include "types.h"


enum class CollisionType
{
    None,   /* All's clear! */
    Dirt,   /* We hit dirt, but that's it. */
    Blocked /* Hit a rock/base/tank/something we can't drive over. */
};

template <typename TCollisionFunc>
concept CollisionEvaluator = requires(TCollisionFunc compute_collision, Direction theoretical_direction,
                                        Position theoretical_position)
{
    {
        compute_collision(theoretical_direction, theoretical_position)
    }
    ->same_as<CollisionType>;
};


class Controllable : public Invalidable
{
 protected:
    Position position;        /* Current tank position */
    Speed speed = {};         /* Velocity... ie: is it moving now? */
    Direction direction = {}; /* Heading of the tank */

    LinkPointSource link_source;
    Reactor reactor;
    MaterialContainer resources;

    std::shared_ptr<Controller> controller = nullptr;

    Level * level = nullptr;

  public:
    Controllable(Position position_, const Reactor & starting_reactor_state, MaterialCapacity material_capacity,
                 Level * level_);
    void SetController(std::shared_ptr<Controller> newController) { this->controller = newController; }

    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] DirectionF GetDirection() const { return this->direction; }
    [[nodiscard]] MaterialContainer & GetResources() { return this->resources; }
    [[nodiscard]] Reactor & GetReactor() { return this->reactor; }
    [[nodiscard]] int GetEnergy() const { return this->reactor.GetEnergy(); }
    [[nodiscard]] int GetHealth() const { return this->reactor.GetHealth(); }
    [[nodiscard]] Level * GetLevel() const { return this->level; };

    [[nodiscard]] bool HealthOrEnergyEmpty() const;

    virtual CollisionType TryCollide(Direction rotation, Position position) = 0;
    virtual void ApplyControllerOutput(ControllerOutput controls) = 0;

    bool HandleMove(DirectionF torch_heading, bool torch_use);
  protected:
  /*  void SetPosition(const Position & new_position) { this->position = new_position; }
    void SetDirection(const Direction & new_direction) { this->direction = new_direction; }*/
};

