#pragma once

#include "ai_controller.h"

class SwarmAI : public AiAlgorithm
{
public:
    ControllerOutput AdvanceStep(const PublicTankInfo & info) override;
};
