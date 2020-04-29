#pragma once

#include "ai_controller.h"
#include "controller.h"

/* Our first AI: Twitch! (note: the 'I' in 'AI' is being used VERY loosely) */

/* The different Twitch travel modes: */
enum class TwitchMode
{
    Start,        /* An init state that picks a direction to leave from. */
    ExitBaseUp,   /* Leave the base in an upward direction. */
    ExitBaseDown, /* Leave the base in a downward direction. */
    Twitch,       /* Do what Twitch does best. */
    Return,       /* Return to base. (Low fuel/health.) */
    Recharge      /* Seek to middle of base, and wait til fully healed. */
};

class TwitchAI final : public AiAlgorithm
{
    class AiController<TwitchAI> * controller;

    /* The "ai" state */
    Speed spd = {};
    bool shoot = false;
    int time_to_change = 0;
    TwitchMode mode = TwitchMode::Start;

  public:
    TwitchAI(AiController<TwitchAI> & controller_) : controller(&controller_) {}


    ControllerOutput AdvanceStep(const PublicTankInfo & info) override;

  private:
    ControllerOutput Return(const PublicTankInfo & info);
    ControllerOutput Recharge(const PublicTankInfo & info);
    ControllerOutput Twitch(const PublicTankInfo & info);
    ControllerOutput ExitDown(const PublicTankInfo & info);
    ControllerOutput ExitUp(const PublicTankInfo & info);
    ControllerOutput Start(const PublicTankInfo & info);
};

