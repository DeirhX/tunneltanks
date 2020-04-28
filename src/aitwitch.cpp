#include "controller.h"
#include "tank.h"
#include "random.h"
#include "tweak.h"
#include "level_view.h"
#include "aitwitch.h"
#include <cstdlib>


/* Used when seeking a base entrance: */
constexpr int Outside = (tweak::base::BaseSize / 2 + 5);


ControllerOutput TwitchAI::AdvanceStep(PublicTankInfo * tank_info)
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
        return {};
    }
}

ControllerOutput TwitchAI::Start(PublicTankInfo * tank_info)
{
    bool no_up = tank_info->level_view.QueryCircle(Offset{0, -Outside + 1}) == LevelView::QueryResult::Collide;
    bool no_down = tank_info->level_view.QueryCircle(Offset{0, Outside - 1}) == LevelView::QueryResult::Collide;

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

ControllerOutput TwitchAI::ExitUp(PublicTankInfo * tank_info)
{
    if (tank_info->y < -Outside)
    { /* Some point outside the base. */
        this->time_to_change = 0;
        this->mode = TwitchMode::Twitch;
        return ControllerOutput{};
    }

    return ControllerOutput{Speed{0, -1}};
}

ControllerOutput TwitchAI::ExitDown(PublicTankInfo * tank_info)
{
    if (tank_info->y > Outside)
    {
        this->time_to_change = 0;
        this->mode = TwitchMode::Twitch;
        return ControllerOutput{};
    }

    return ControllerOutput{Speed{0, 1}};
}

ControllerOutput TwitchAI::Twitch(PublicTankInfo * tank_info)
{
    if (tank_info->health.amount < tweak::tank::StartingShield.amount / 2 ||
        tank_info->energy.amount < tweak::tank::StartingEnergy.amount / 3 ||
        (abs(tank_info->x) < tweak::base::BaseSize / 2 && abs(tank_info->y) < tweak::base::BaseSize / 2))
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
    return ControllerOutput{Speed{this->spd}, this->shoot};
}
/* Make a simple effort to get back to your base: */
ControllerOutput TwitchAI::Return(PublicTankInfo * tank_info)
{
    /* Seek to the closest entrance: */
    int targety = (tank_info->y < 0) ? -Outside : Outside;

    /* Check to see if we've gotten there: */
    if ((tank_info->x == 0 && tank_info->y == targety) ||
        (abs(tank_info->x) < tweak::base::BaseSize / 2 && abs(tank_info->y) < tweak::base::BaseSize / 2))
    {
        this->mode = TwitchMode::Recharge;
        return {};
    }

    /* If we are close to the base, we need to navigate around the walls: */
    if (abs(tank_info->x) <= Outside && abs(tank_info->y) < Outside)
    {
        return {Speed{0, (tank_info->y < targety) * 2 - 1}};
    }

    /* Else, we will very simply seek to the correct point: */
    Speed speed;
    speed.x = tank_info->x != 0 ? ((tank_info->x < 0) * 2 - 1) : 0;
    speed.y = tank_info->y != targety ? ((tank_info->y < targety) * 2 - 1) : 0;
    return {speed};
}

ControllerOutput TwitchAI::Recharge(PublicTankInfo * tank_info)
{
    /* Check to see if we're fully charged/healed: */
    if (tank_info->health == tweak::tank::StartingShield && tank_info->energy == tweak::tank::StartingEnergy)
    {
        this->mode = TwitchMode::Start;
        return {};
    }

    /* Else, seek to the base's origin, and wait: */
    return {Speed{
        tank_info->x ? ((tank_info->x < 0) * 2 - 1) : 0,
        tank_info->y ? ((tank_info->y < 0) * 2 - 1) : 0}};
}

ControllerOutput TwitchController::ApplyControls(struct PublicTankInfo * tank_info)
{
    return this->the_ai.AdvanceStep(tank_info);
}