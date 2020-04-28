#pragma once

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

class TwitchAI
{
    class TwitchController * controller;

    /* The "ai" state */
    Speed spd = {};
    bool shoot = false;
    int time_to_change = 0;
    TwitchMode mode = TwitchMode::Start;

  public:
    TwitchAI(TwitchController & controller_) : controller(&controller_) {}

    ControllerOutput AdvanceStep(PublicTankInfo * info);

  private:
    ControllerOutput Return(PublicTankInfo * info);
    ControllerOutput Recharge(PublicTankInfo * info);
    ControllerOutput Twitch(PublicTankInfo * info);
    ControllerOutput ExitDown(PublicTankInfo * info);
    ControllerOutput ExitUp(PublicTankInfo * info);
    ControllerOutput Start(PublicTankInfo * info);
};

class TwitchController : public Controller
{
  private:
    TwitchAI the_ai;

  public:
    TwitchController() : the_ai(*this) {}
    ControllerOutput ApplyControls(struct PublicTankInfo * tank_info) override;

    bool IsPlayer() override { return false; }
};
