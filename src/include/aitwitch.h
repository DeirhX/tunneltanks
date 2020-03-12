#pragma once


/* Our first AI: Twitch! (note: the 'I' in 'AI' is being used VERY loosely) */

/* The different Twitch travel modes: */
typedef enum TwitchMode {
	TWITCH_START,     /* An init state that picks a direction to leave from. */
	TWITCH_EXIT_UP,   /* Leave the base in an upward direction. */
	TWITCH_EXIT_DOWN, /* Leave the base in a downward direction. */
	TWITCH_TWITCH,    /* Do what Twitch does best. */
	TWITCH_RETURN,    /* Return to base. (Low fuel/health.) */
	TWITCH_RECHARGE   /* Seek to middle of base, and wait til fully healed. */
} TwitchMode;

struct TwitchController : public Controller
{
public:
	Speed spd;
	bool  shoot;
	int time_to_change;
	TwitchMode    mode;
public:
	TwitchController();
	ControllerOutput ApplyControls(struct PublicTankInfo* tankPublic) override;
};
