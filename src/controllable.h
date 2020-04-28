#pragma once
#include "controller.h"
#include "link.h"
#include "types.h"

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

    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] DirectionF GetDirection() const { return this->direction; }
    [[nodiscard]] MaterialContainer & GetResources() { return this->resources; }
    [[nodiscard]] Reactor & GetReactor() { return this->reactor; }
    [[nodiscard]] int GetEnergy() const { return this->reactor.GetEnergy(); }
    [[nodiscard]] int GetHealth() const { return this->reactor.GetHealth(); }
    [[nodiscard]] Level * GetLevel() const { return this->level; };

    [[nodiscard]] bool HealthOrEnergyEmpty() const;

    void SetController(std::shared_ptr<Controller> newController) { this->controller = newController; }

  protected:
  /*  void SetPosition(const Position & new_position) { this->position = new_position; }
    void SetDirection(const Direction & new_direction) { this->direction = new_direction; }*/
};
