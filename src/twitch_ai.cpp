#include "twitch_ai.h"

#include "controller.h"
#include "tank.h"
#include "random.h"
#include "tweak.h"
#include "level_view.h"

#include <cstdlib>


/* Used when seeking a base entrance: */
constexpr int OutsideBase = (tweak::base::BaseSize / 2 + 5);


ControllerOutput TwitchAI::AdvanceStep(const PublicTankInfo & tank_info)
{
    switch (this->mode)
    {
    case TwitchMode::Start:
        return this->Start(tank_info);
    case TwitchMode::ExitBaseUp:
        return this->ExitUp(tank_info);
    case TwitchMode::ExitBaseDown:
        return this->ExitDown(tank_info);
    case TwitchMode::Twitch:
        return this->Twitch(tank_info);
    case TwitchMode::Return:
        return this->Return(tank_info);
    case TwitchMode::Recharge:
        return this->Recharge(tank_info);
    default:
        assert(!"Unknown state");
        return {};
    }
}

ControllerOutput TwitchAI::Start(const PublicTankInfo & tank_info)
{
    bool no_up = tank_info.level_view.QueryCircle(Offset{0, -OutsideBase + 1}) == LevelView::QueryResult::Collide;
    bool no_down = tank_info.level_view.QueryCircle(Offset{0, OutsideBase - 1}) == LevelView::QueryResult::Collide;

    if (no_up && no_down)
    {
        /* TODO: Make it so that this condition isn't possible... */
        this->mode = Random.Bool(500) ? TwitchMode::ExitBaseUp : TwitchMode::ExitBaseDown;
    }
    else if (no_up)
    {
        this->mode = TwitchMode::ExitBaseDown;
    }
    else if (no_down)
    {
        this->mode = TwitchMode::ExitBaseUp;
    }
    else
        this->mode = Random.Bool(500) ? TwitchMode::ExitBaseUp : TwitchMode::ExitBaseDown;
    return {};
}

ControllerOutput TwitchAI::ExitUp(const PublicTankInfo & tank_info)
{
    if (tank_info.relative_pos.y < -OutsideBase)
    { /* Some point outside the base. */
        this->time_to_change = 0;
        this->mode = TwitchMode::Twitch;
        return ControllerOutput{};
    }

    return ControllerOutput{Speed{0, -1}};
}

ControllerOutput TwitchAI::ExitDown(const PublicTankInfo & tank_info)
{
    if (tank_info.relative_pos.y > OutsideBase)
    {
        this->time_to_change = 0;
        this->mode = TwitchMode::Twitch;
        return ControllerOutput{};
    }

    return ControllerOutput{Speed{0, 1}};
}

ControllerOutput TwitchAI::Twitch(const PublicTankInfo & tank_info)
{
    if (tank_info.health.amount < tweak::tank::StartingShield.amount / 2 ||
        tank_info.energy.amount < tweak::tank::StartingEnergy.amount / 3 ||
        (abs(tank_info.relative_pos.x) < tweak::base::BaseSize / 2 &&
         abs(tank_info.relative_pos.y) < tweak::base::BaseSize / 2))
    {
        /* We need a quick pick-me-up... */
        this->mode = TwitchMode::Return;
    }

    if (!this->time_to_change)
    {
        this->time_to_change = Random.Int(10u, 30u);
        this->spd.x = Random.Int(0, 2) - 1;
        this->spd.y = Random.Int(0, 2) - 1;
        this->shoot = Random.Bool(300);
    }

    this->time_to_change--;
    return ControllerOutput{.speed = Speed{this->spd}, .is_shooting_primary = this->shoot};
}
/* Make a simple effort to get back to your base: */
ControllerOutput TwitchAI::Return(const PublicTankInfo & tank_info)
{
    /* Seek to the closest entrance: */
    int targety = (tank_info.relative_pos.y < 0) ? -OutsideBase : OutsideBase;

    /* Check to see if we've gotten there: */
    if ((tank_info.relative_pos.x == 0 && tank_info.relative_pos.y == targety) ||
        (abs(tank_info.relative_pos.x) < tweak::base::BaseSize / 2 &&
         abs(tank_info.relative_pos.y) < tweak::base::BaseSize / 2))
    {
        this->mode = TwitchMode::Recharge;
        return {};
    }

    /* If we are close to the base, we need to navigate around the walls: */
    if (abs(tank_info.relative_pos.x) <= OutsideBase && abs(tank_info.relative_pos.y) < OutsideBase)
    {
        return {Speed{0, (tank_info.relative_pos.y < targety) * 2 - 1}};
    }

    /* Else, we will very simply seek to the correct point: */
    Speed speed;
    speed.x = tank_info.relative_pos.x != 0 ? ((tank_info.relative_pos.x < 0) * 2 - 1) : 0;
    speed.y = tank_info.relative_pos.y != targety ? ((tank_info.relative_pos.y < targety) * 2 - 1) : 0;
    return {speed};
}

ControllerOutput TwitchAI::Recharge(const PublicTankInfo & tank_info)
{
    /* Check to see if we're fully charged/healed: */
    if (tank_info.health == tweak::tank::StartingShield && tank_info.energy == tweak::tank::StartingEnergy)
    {
        this->mode = TwitchMode::Start;
        return {};
    }

    /* Else, seek to the base's origin, and wait: */
    return {Speed{tank_info.relative_pos.x ? ((tank_info.relative_pos.x < 0) * 2 - 1) : 0,
                  tank_info.relative_pos.y ? ((tank_info.relative_pos.y < 0) * 2 - 1) : 0}};
}
