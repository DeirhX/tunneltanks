#pragma once

#include "ai_controller.h"
namespace crust
{

class SwarmAI : public AiAlgorithm
{
  public:
    ControllerOutput AdvanceStep(const PublicTankInfo & info) override;
};

} // namespace crust