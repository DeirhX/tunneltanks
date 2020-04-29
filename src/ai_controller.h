#pragma once
#include "controller.h"

struct PublicTankInfo;

class AiAlgorithm
{
public:
    ~AiAlgorithm() = default;

    virtual ControllerOutput AdvanceStep(const PublicTankInfo & info) = 0;
};


template <typename AiType>
class AiController : public Controller
{
  private:
    AiType ai_engine;

  public:
    AiController() : ai_engine(*this) {}
    ControllerOutput ApplyControls(const PublicTankInfo & tank_info) override
    {
        return this->ai_engine.AdvanceStep(tank_info);
    }

    bool IsPlayer() override { return false; }
};
