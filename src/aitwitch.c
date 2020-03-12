#include <cstdlib>

#include <controller.h>
#include <tank.h>
#include <memalloc.h>
#include <random.h>
#include <tweak.h>
#include "LevelView.h"
#include "aitwitch.h"


/* Used when seeking a base entrance: */
#define OUTSIDE (BASE_SIZE/2 + 5)


TwitchController::TwitchController()
{
	this->mode = TWITCH_START;
}


ControllerOutput do_start(PublicTankInfo *i, TwitchController *data) {
	int no_up, no_down;
	
	no_up = i->level_view.QueryCircle(Offset{ 0, -OUTSIDE + 1 }) == LevelView::QueryResult::Collide;
	no_down = i->level_view.QueryCircle(Offset{ 0,  OUTSIDE - 1 }) == LevelView::QueryResult::Collide;
	
	if(no_up && no_down) {
		/* TODO: Make it so that this condition isn't possible... */
		data->mode = Random.Bool(500) ? TWITCH_EXIT_UP : TWITCH_EXIT_DOWN;
	} else if(no_up) {
		data->mode = TWITCH_EXIT_DOWN;
	} else if(no_down) {
		data->mode = TWITCH_EXIT_UP;
	} else
		data->mode = Random.Bool(500) ? TWITCH_EXIT_UP : TWITCH_EXIT_DOWN;
	return { };
}

ControllerOutput do_exit_up(PublicTankInfo *i, TwitchController* data) 
{
	
	if(i->y < -OUTSIDE) { /* Some point outside the base. */
		data->time_to_change = 0;
		data->mode = TWITCH_TWITCH;
		return ControllerOutput{ };
	}

	return ControllerOutput{ Speed {0, -1} };
}

ControllerOutput do_exit_down(PublicTankInfo *i, TwitchController* data) {
	
	if(i->y > OUTSIDE) {
		data->time_to_change = 0;
		data->mode = TWITCH_TWITCH;
		return ControllerOutput{ };
	}
	
	return ControllerOutput{ Speed {0, 1} };
}

ControllerOutput do_twitch(PublicTankInfo *i, TwitchController* data) {
	
	if(i->health < TANK_STARTING_SHIELD/2 || i->energy < TANK_STARTING_FUEL/3 ||
	  (abs(i->x) < BASE_SIZE/2 && abs(i->y) < BASE_SIZE/2) ) {
		/* We need a quick pick-me-up... */
		data->mode = TWITCH_RETURN;
	}
	
	if(!data->time_to_change) {
		data->time_to_change = Random.Int(10u, 30u);
		data->spd.x = Random.Int(0,2) - 1;
		data->spd.y = Random.Int(0,2) - 1;
		data->shoot  = Random.Bool(300);
	}
	
	data->time_to_change--;
	return ControllerOutput{ Speed {data->spd}, data->shoot };
}

/* Make a simple effort to get back to your base: */
ControllerOutput do_return(PublicTankInfo *i, TwitchController* data) {
	
	/* Seek to the closest entrance: */
	int targety = (i->y < 0) ? -OUTSIDE : OUTSIDE;
	
	/* Check to see if we've gotten there: */
	if((i->x == 0 && i->y == targety) || 
	   (abs(i->x)<BASE_SIZE/2 && abs(i->y)<BASE_SIZE/2)) {
		data->mode = TWITCH_RECHARGE;
		return { };
	}
	
	/* If we are close to the base, we need to navigate around the walls: */
	if( abs(i->x) <= OUTSIDE && abs(i->y) < OUTSIDE ) {
		return { Speed { 0, (i->y < targety) * 2 - 1 } };
	}
	
	/* Else, we will very simply seek to the correct point: */
	Speed speed;
	speed.x = i->x != 0       ? ((i->x < 0) * 2 - 1) : 0;
	speed.y = i->y != targety ? ((i->y < targety) * 2 - 1) : 0;
	return { speed };
}

ControllerOutput do_recharge(PublicTankInfo *i, TwitchController* data) {
	
	/* Check to see if we're fully charged/healed: */
	if(i->health == TANK_STARTING_SHIELD && i->energy == TANK_STARTING_FUEL) {
		data->mode = TWITCH_START;
		return { };
	}
	
	/* Else, seek to the base's origin, and wait: */
	return { 
		Speed {
			i->x ? ((i->x < 0) * 2 - 1) : 0,
			i->y ? ((i->y < 0) * 2 - 1) : 0 }
	};
}


ControllerOutput TwitchController::ApplyControls(struct PublicTankInfo* i)
{
	switch (this->mode) {
	case TWITCH_START:     return do_start(i, this); 
	case TWITCH_EXIT_UP:   return do_exit_up(i, this); 
	case TWITCH_EXIT_DOWN: return do_exit_down(i, this); 
	case TWITCH_TWITCH:    return do_twitch(i, this); 
	case TWITCH_RETURN:    return do_return(i, this);
	case TWITCH_RECHARGE:  return do_recharge(i, this); 
	default: return { };
	}
}