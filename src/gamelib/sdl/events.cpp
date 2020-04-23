#include "gamelib.h"
#include "types.h"
#include "require_sdl.h"
#include <sdl2/include/SDL.h>


#define FULLSCREEN_KEY   SDLK_F10
#define EXIT_KEY         SDLK_ESCAPE

/* TODO: Move this into the _DATA structure? Maybe... */
typedef struct Event {
	SDL_Event e;
	GameEvent type;
	Rect      dim;
} Event;

static Event cur_event = {
	/*.e         = NULL,*/
	.type      = GameEvent::None,
	.dim = {0,0,0,0}
};


/* Will check the SDL event stack for a new event, if we don't have one... */
static void check_for_event() {
	
	/* Don't do jack if the current event hasn't been released: */
	if(cur_event.type != GameEvent::None) return;
	
	/* Grab the next event (if it exists) off the stack: */
	while(cur_event.type == GameEvent::None) {
		
        /* Grab the next event, and only continue if we got something: */
        SDL_Event event;
        if (!SDL_PollEvent(&event))
            break;
		
		/* Resize event: */
		if(event.type == SDL_WINDOWEVENT && event.window.event == SDL_WINDOWEVENT_RESIZED) {
			cur_event.type = GameEvent::Resize;
            cur_event.dim = Rect(0, 0, event.window.data1, event.window.data2);
		
		/* Keyboard events: */
		} else if(event.type == SDL_KEYDOWN) {
			if(event.key.keysym.sym == FULLSCREEN_KEY)
				cur_event.type = GameEvent::ToggleFullscreen;
			
			else if(event.key.keysym.sym == EXIT_KEY)
				cur_event.type = GameEvent::Exit;
		
		/* Window close event: */
		} else if(event.type == SDL_QUIT)
			cur_event.type = GameEvent::Exit;
	}
}


GameEvent gamelib_event_get_type() {
	check_for_event();
	
	return cur_event.type;
}

Rect gamelib_event_resize_get_size() {
	check_for_event();
	
	if(cur_event.type != GameEvent::Resize) return Rect(0,0,0,0);
	return cur_event.dim;
}

void gamelib_event_done() {
	cur_event.type = GameEvent::None;
}
